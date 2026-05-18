using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace DFM.JiraSync
{
    /// <summary>
    /// Req6: Streaming, incremental JIRA sync for DMGT + DIBITP.
    /// Key differences vs Req5:
    ///   * Per-batch SqlConnection (no per-row connect/close storm)
    ///   * Includes JiraID in INSERT (fixes "Cannot insert NULL into JiraID")
    ///   * Streams batch-by-batch (no large in-memory accumulation)
    ///   * Pulls extra fields needed for the redesigned dashboard
    ///   * Emits a compact console-exec-summary.log with ~10-15 scenarios
    ///   * After sync runs sp_RefreshDashboardReport so the web dashboard
    ///     reads pre-aggregated numbers and loads "in the blink of an eye".
    /// </summary>
    internal class Program
    {
        private static readonly string ConnStr =
            ConfigurationManager.ConnectionStrings["DFM"].ConnectionString;
        private static readonly string BaseUrl =
            ConfigurationManager.AppSettings["JiraBaseUrl"];
        private static readonly string JiraUser =
            ConfigurationManager.AppSettings["JiraUser"];
        private static readonly string JiraPass =
            ConfigurationManager.AppSettings["JiraPassword"];
        private static readonly int BatchSize =
            int.Parse(ConfigurationManager.AppSettings["BatchSize"] ?? "1000");
        private static readonly int MinBatchSize =
            int.Parse(ConfigurationManager.AppSettings["MinBatchSize"] ?? "100");
        private static readonly int HttpTimeoutMs =
            int.Parse(ConfigurationManager.AppSettings["HttpTimeoutSeconds"] ?? "600") * 1000;
        private static readonly int MaxFetchAttempts =
            int.Parse(ConfigurationManager.AppSettings["MaxFetchAttempts"] ?? "3");
        private static readonly string FieldsOverride =
            ConfigurationManager.AppSettings["JiraFields"]; // null => use default heavy list; "" => let Jira pick defaults
        private static readonly string[] Projects =
            (ConfigurationManager.AppSettings["Projects"] ?? "DMGT,DIBITP").Split(',');
        private static readonly string CsvOut =
            ConfigurationManager.AppSettings["OutputCsvPath"] ?? "JIRADashboard.csv";

        private static int _inserted, _updated, _failed, _pulled;
        private static int _batchCount;
        private static readonly Dictionary<string, int> _scenarioCounts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> _scenarioSamples = new List<string>();

        public static int Main(string[] args)
        {
            DateTime start = DateTime.Now;
            EnsureLogDir();
            Log("===== DFM JIRA Sync started at " + start.ToString("yyyy-MM-dd HH:mm:ss") + " =====");
            RecordScenario("SyncStarted", "Projects=" + string.Join(",", Projects) + ", BatchSize=" + BatchSize);

            int syncId = StartSyncLog(start);

            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                foreach (string p in Projects)
                {
                    string proj = p.Trim();
                    if (proj.Length == 0) continue;
                    Log("--- Syncing project: " + proj + " ---");
                    SyncProject(proj);
                }

                Log("Applying DMGT->DIBITP hierarchy inheritance...");
                ExecSp("sp_ApplyHierarchyInheritance");
                RecordScenario("HierarchyApplied",
                    "DMGT->DIBITP fields inherited (Chief/PM/TL/Platform/Portfolio/DemandType/DemandOwner)");

                Log("Refreshing pre-aggregated DashboardSummary...");
                try { ExecSp("sp_RefreshDashboardSummary"); }
                catch (Exception ex) { Log("  sp_RefreshDashboardSummary failed (continuing): " + ex.Message);
                                        RecordScenario("LegacySummaryFailed", ex.Message); }

                Log("Refreshing Req6 DashboardReport (one-shot dashboard payload)...");
                ExecSp("sp_RefreshDashboardReport");
                RecordScenario("DashboardReportRefreshed",
                    "Pre-aggregated KPIs/Demand/Platform/Portfolio/RAG/Aging/PM/TL/Chief/MarketTrending");

                Log("Exporting JIRADashboard.csv...");
                ExportCsv();

                DateTime end = DateTime.Now;
                FinishSyncLog(syncId, end, "Success", null);
                RecordScenario("SyncCompleted",
                    string.Format("Pulled={0}, Inserted={1}, Updated={2}, Failed={3}, Duration={4}",
                                  _pulled, _inserted, _updated, _failed, end - start));
                Log(string.Format("===== Completed. Pulled={0}, Inserted={1}, Updated={2}, Failed={3}. Duration={4} =====",
                    _pulled, _inserted, _updated, _failed, end - start));
                WriteExecSummary("Success");
                return 0;
            }
            catch (Exception ex)
            {
                Log("FATAL: " + ex);
                RecordScenario("SyncFailed", ex.Message);
                FinishSyncLog(syncId, DateTime.Now, "Failed", ex.ToString());
                WriteExecSummary("Failed");
                return 1;
            }
        }

        // ====================================================================
        // Streaming sync - fetch a batch, write it, drop references, repeat.
        // ====================================================================
        private static void SyncProject(string projectKey)
        {
            int startAt = 0;
            int total = int.MaxValue;
            int batchIndex = 0;
            int currentBatch = BatchSize;

            string defaultFields = "summary,status,issuetype,priority,project,created,updated,duedate,assignee,reporter,issuelinks,"
                + "customfield_12609,customfield_12604,customfield_11610,customfield_13380,"
                + "customfield_13419,customfield_13511,customfield_13510,customfield_13505,customfield_13509,"
                + "customfield_13317,customfield_13358,customfield_13357,customfield_14001,"
                + "customfield_12603,customfield_13339,customfield_13379,customfield_13376,"
                + "customfield_13306,customfield_13359,customfield_10964,"
                + "customfield_13375,customfield_13374,customfield_13362,"
                + "customfield_13307,customfield_13308,"
                + "customfield_10043,customfield_10044";
            // FieldsOverride == null  -> use the heavy default list above
            // FieldsOverride == ""    -> omit fields entirely (matches the working ASPX behaviour)
            // otherwise               -> use whatever the operator configured
            string fields = FieldsOverride == null ? defaultFields : FieldsOverride;

            while (startAt < total)
            {
                batchIndex++;
                string jql = "project=" + projectKey + " ORDER BY updated DESC";

                string body = null;
                int attempt = 0;
                while (attempt < MaxFetchAttempts && body == null)
                {
                    attempt++;
                    try
                    {
                        body = JiraSearch(jql, startAt, currentBatch, fields);
                    }
                    catch (WebException wex)
                    {
                        bool isTimeout = wex.Status == WebExceptionStatus.Timeout
                                      || wex.Status == WebExceptionStatus.ReceiveFailure
                                      || wex.Status == WebExceptionStatus.ConnectFailure
                                      || wex.Status == WebExceptionStatus.KeepAliveFailure;
                        Log(string.Format("  Batch fetch attempt {0}/{1} failed ({2} startAt={3} size={4}): {5} [{6}]",
                            attempt, MaxFetchAttempts, projectKey, startAt, currentBatch, wex.Message, wex.Status));
                        RecordScenario(isTimeout ? "ApiFetchTimeout" : "ApiFetchFailed",
                            projectKey + " startAt=" + startAt + " size=" + currentBatch + " :: " + wex.Status + " " + wex.Message);

                        if (attempt >= MaxFetchAttempts) break;

                        // On timeout/receive failures: shrink the batch and back off, then retry the SAME startAt.
                        if (isTimeout && currentBatch > MinBatchSize)
                        {
                            int next = Math.Max(MinBatchSize, currentBatch / 2);
                            Log("    Shrinking batch size " + currentBatch + " -> " + next + " and retrying.");
                            currentBatch = next;
                        }
                        try { System.Threading.Thread.Sleep(2000 * attempt); } catch { }
                    }
                    catch (Exception ex)
                    {
                        Log("  Batch fetch failed (non-retryable) (" + projectKey + " startAt=" + startAt + "): " + ex.Message);
                        RecordScenario("ApiFetchFailed", projectKey + " startAt=" + startAt + " :: " + ex.Message);
                        break;
                    }
                }

                if (body == null)
                {
                    Log("  Giving up on startAt=" + startAt + " for project " + projectKey + " after " + MaxFetchAttempts + " attempts.");
                    // Skip past this slice rather than aborting the whole project, so subsequent
                    // batches still get processed. The next sync run will retry the gap.
                    startAt += currentBatch;
                    continue;
                }

                LogResponse(projectKey, startAt, body);

                var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                Dictionary<string, object> obj;
                try { obj = ser.Deserialize<Dictionary<string, object>>(body); }
                catch (Exception ex)
                {
                    Log("  Batch parse failed: " + ex.Message);
                    RecordScenario("ApiParseFailed", projectKey + " startAt=" + startAt + " :: " + ex.Message);
                    break;
                }
                if (obj == null || !obj.ContainsKey("issues")) break;

                var issues = obj["issues"] as ArrayList;
                if (issues == null || issues.Count == 0) break;

                total = obj.ContainsKey("total") ? Convert.ToInt32(obj["total"]) : issues.Count;

                Log(string.Format("  Batch #{0} startAt={1} fetched={2} total={3}",
                                  batchIndex, startAt, issues.Count, total));
                RecordScenario("BatchFetched",
                    string.Format("{0} #{1} startAt={2} got={3} total={4}",
                                  projectKey, batchIndex, startAt, issues.Count, total));

                int batchInserted = 0, batchUpdated = 0, batchFailed = 0;
                using (var con = new SqlConnection(ConnStr))
                {
                    con.Open();
                    using (var cmd = BuildUpsertCommand(con))
                    {
                        foreach (Dictionary<string, object> issue in issues)
                        {
                            try
                            {
                                int r = UpsertIssue(cmd, issue, projectKey);
                                if (r == 1) { _inserted++; batchInserted++; }
                                else        { _updated++;  batchUpdated++; }
                                _pulled++;
                            }
                            catch (Exception ex)
                            {
                                _failed++; batchFailed++;
                                string key = issue.ContainsKey("key") ? issue["key"].ToString() : "?";
                                Log("  Upsert failed for " + key + ": " + ex.Message);
                                if (ex.Message.IndexOf("NULL", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                    ex.Message.IndexOf("JiraID", StringComparison.OrdinalIgnoreCase) >= 0)
                                    RecordScenario("UpsertNullJiraID", key + " :: " + ex.Message);
                                else
                                    RecordScenario("UpsertFailed", key + " :: " + ex.Message);
                            }
                        }
                    }
                }

                _batchCount++;
                Log(string.Format("    Batch #{0} -> Inserted={1} Updated={2} Failed={3}",
                                  batchIndex, batchInserted, batchUpdated, batchFailed));

                int fetched = issues.Count;
                startAt += fetched;
                // help GC drop the body / issues collection before the next batch
                issues.Clear();
                obj = null; body = null;

                if (startAt >= total || fetched < currentBatch) break;

                // Successful batch: gradually grow back toward the configured size for throughput.
                if (currentBatch < BatchSize)
                {
                    int grown = Math.Min(BatchSize, currentBatch * 2);
                    if (grown != currentBatch)
                    {
                        Log("    Recovering batch size " + currentBatch + " -> " + grown);
                        currentBatch = grown;
                    }
                }
            }
        }

        // ====================================================================
        // Build a reusable parameterised upsert command (one per batch).
        // Includes JiraID so the NOT-NULL constraint never fires (Req6 fix).
        // ====================================================================
        private static SqlCommand BuildUpsertCommand(SqlConnection con)
        {
            var cmd = con.CreateCommand();
            cmd.CommandTimeout = 150;
            cmd.CommandText = @"
SET NOCOUNT ON;
IF EXISTS (SELECT 1 FROM JiraIssues WHERE JiraKey=@k)
BEGIN
    UPDATE JiraIssues
       SET JiraID = @jid,
           Summary=@summary, Status=@status, OverallStatus=@status, Priority=@priority,
           IssueType=@itype, ProjectType=@itype,
           ProjectKey=@proj, ProjectName=@projName, ParentJiraID=@parent,
           Platform=@plat, PlatformVertical=@pv, PlatformName=@pn, SecondaryPlatform=@sp,
           ActivityRagStatus=@arag, ScheduleRag=@srag, BudgetRag=@brag, RaidRag=@raidr,
           OverallProjectRag=@orag, ProjectRAG=ISNULL(@computedRag, ProjectRAG),
           ChiefNameMapping=@chief,
           Manager=@mgr,
           TechLead=@tl,
           AccountableExecLead=@ael,
           SmeLead=@sme,
           AccountableExec=@ae,
           Sponsor=@ae,
           Stakeholder=@ael,
           Assignee=@assignee,
           Reporter=@reporter,
           AssignedProjectManager=@apm,
           IdhPortfolioHead=@iph,
           DemandOwner=@downer,
           DemandType=@dtype,
           TargetCompletionDate=@tcd,
           Target_Completion_Date=@tcd,
           ProposedDemandPickupDate=@pdp,
           Proposed_Demand_Pick_up_Date=@pdp,
           Actual_Go_Live_Date=@agld,
           Proposed_Baseline_0_End_Date=@pb0e,
           Proposed_Baseline_0_Start_Date=@pb0s,
           Proposed_Baseline_0_submission_Date=@pb0sb,
           Primary_Classification=@pcl,
           Classification=@cl,
           Department=@dep,
           JiraCreated=ISNULL(JiraCreated, @cre),
           JiraUpdated=@upd,
           CreatedDate=ISNULL(CreatedDate,@cre),
           UpdatedDate=@upd
     WHERE JiraKey=@k;
    SELECT 0;
END
ELSE
BEGIN
    INSERT INTO JiraIssues(
        JiraID, JiraKey, Summary, Status, OverallStatus, Priority, IssueType, ProjectType,
        ProjectKey, ProjectName, ParentJiraID,
        Platform, PlatformVertical, PlatformName, SecondaryPlatform,
        ActivityRagStatus, ScheduleRag, BudgetRag, RaidRag,
        OverallProjectRag, ProjectRAG,
        ChiefNameMapping, Manager, TechLead,
        AccountableExecLead, SmeLead, AccountableExec, Sponsor, Stakeholder,
        Assignee, Reporter,
        AssignedProjectManager, IdhPortfolioHead, DemandOwner, DemandType,
        TargetCompletionDate, Target_Completion_Date,
        ProposedDemandPickupDate, Proposed_Demand_Pick_up_Date,
        Actual_Go_Live_Date,
        Proposed_Baseline_0_End_Date, Proposed_Baseline_0_Start_Date,
        Proposed_Baseline_0_submission_Date,
        Primary_Classification, Classification, Department,
        JiraCreated, JiraUpdated, CreatedDate, UpdatedDate)
    VALUES(
        @jid, @k, @summary, @status, @status, @priority, @itype, @itype,
        @proj, @projName, @parent,
        @plat, @pv, @pn, @sp,
        @arag, @srag, @brag, @raidr,
        @orag, @computedRag,
        @chief, @mgr, @tl,
        @ael, @sme, @ae, @ae, @ael,
        @assignee, @reporter,
        @apm, @iph, @downer, @dtype,
        @tcd, @tcd,
        @pdp, @pdp,
        @agld,
        @pb0e, @pb0s, @pb0sb,
        @pcl, @cl, @dep,
        @cre, @upd, @cre, @upd);
    SELECT 1;
END";
            cmd.Parameters.Add("@jid",      SqlDbType.NVarChar, 50);
            cmd.Parameters.Add("@k",        SqlDbType.NVarChar, 50);
            cmd.Parameters.Add("@summary",  SqlDbType.NVarChar, 500);
            cmd.Parameters.Add("@status",   SqlDbType.NVarChar, 100);
            cmd.Parameters.Add("@priority", SqlDbType.NVarChar, 50);
            cmd.Parameters.Add("@itype",    SqlDbType.NVarChar, 100);
            cmd.Parameters.Add("@proj",     SqlDbType.NVarChar, 20);
            cmd.Parameters.Add("@projName", SqlDbType.NVarChar, 300);
            cmd.Parameters.Add("@parent",   SqlDbType.NVarChar, 50);
            cmd.Parameters.Add("@plat",     SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@pv",       SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@pn",       SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@sp",       SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@arag",     SqlDbType.NVarChar, 50);
            cmd.Parameters.Add("@srag",     SqlDbType.NVarChar, 50);
            cmd.Parameters.Add("@brag",     SqlDbType.NVarChar, 50);
            cmd.Parameters.Add("@raidr",    SqlDbType.NVarChar, 50);
            cmd.Parameters.Add("@orag",     SqlDbType.NVarChar, 50);
            cmd.Parameters.Add("@computedRag", SqlDbType.NVarChar, 50);
            cmd.Parameters.Add("@chief",    SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@mgr",      SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@tl",       SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@ael",      SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@sme",      SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@ae",       SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@assignee", SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@reporter", SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@apm",      SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@iph",      SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@downer",   SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@dtype",    SqlDbType.NVarChar, 100);
            cmd.Parameters.Add("@tcd",      SqlDbType.DateTime);
            cmd.Parameters.Add("@pdp",      SqlDbType.DateTime);
            cmd.Parameters.Add("@agld",     SqlDbType.DateTime);
            cmd.Parameters.Add("@pb0e",     SqlDbType.DateTime);
            cmd.Parameters.Add("@pb0s",     SqlDbType.DateTime);
            cmd.Parameters.Add("@pb0sb",    SqlDbType.DateTime);
            cmd.Parameters.Add("@pcl",      SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@cl",       SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@dep",      SqlDbType.NVarChar, 200);
            cmd.Parameters.Add("@cre",      SqlDbType.DateTime);
            cmd.Parameters.Add("@upd",      SqlDbType.DateTime);
            return cmd;
        }

        private static int UpsertIssue(SqlCommand cmd, Dictionary<string, object> issue, string projectKey)
        {
            string key = issue.ContainsKey("key") && issue["key"] != null ? issue["key"].ToString() : null;
            if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("Issue missing 'key'");

            // Per requirement: both JiraID and JiraKey store the issue key (e.g. DMGT-202)
            string id = key;

            var fields = issue["fields"] as Dictionary<string, object>;
            if (fields == null) throw new InvalidOperationException("Issue " + key + " has no 'fields'");

            string summary    = Get(fields, "summary");
            string status     = GetNested(fields, "status", "name");
            string priority   = GetNested(fields, "priority", "name");
            string issuetype  = GetNested(fields, "issuetype", "name");
            string projName   = GetNested(fields, "project", "name");
            string parentJid  = ExtractParentKey(fields);

            // Platform fields (console-specific custom fields)
            string platform         = GetNestedAny(fields, "customfield_12609", new[] { "value", "name" });
            string platformVertical = GetNestedAny(fields, "customfield_12604", new[] { "value", "name" });
            string platformName     = GetNestedAny(fields, "customfield_11610", new[] { "value", "name" });
            string secPlatform      = GetNestedAny(fields, "customfield_13380", new[] { "value", "name" });

            // Platform fallbacks from ASPX (customfield_10043, 10044, or scan)
            if (string.IsNullOrEmpty(platform))
                platform = GetNestedAny(fields, "customfield_10043", new[] { "value", "name" });
            if (string.IsNullOrEmpty(platform))
                platform = GetNestedAny(fields, "customfield_10044", new[] { "value", "name" });
            if (string.IsNullOrEmpty(platform))
                platform = ScanForPlatform(fields);

            // RAG fields
            string activityRag      = GetNestedAny(fields, "customfield_13419", new[] { "value", "name" });
            string scheduleRag      = GetNestedAny(fields, "customfield_13511", new[] { "value", "name" });
            string budgetRag        = GetNestedAny(fields, "customfield_13510", new[] { "value", "name" });
            string raidRag          = GetNestedAny(fields, "customfield_13505", new[] { "value", "name" });
            string overallRag       = GetNestedAny(fields, "customfield_13509", new[] { "value", "name" });

            // People fields - aligned with JiraIntegration.aspx.cs mappings:
            //   customfield_13357 = AccountableExecLead (Stakeholder)
            //   customfield_13358 = SmeLead (TechLead)
            //   customfield_13379 = AccountableExec (Sponsor)
            //   customfield_13376 = IdhPortfolioHead
            //   customfield_12603 = AssignedProjectManager (Manager)
            //   customfield_13317 = DemandOwner
            //   customfield_14001 = ChiefNameMapping
            string accExecLead      = GetNested(fields, "customfield_13357", "displayName");
            string smeLead          = GetNested(fields, "customfield_13358", "displayName");
            string accExec          = GetNested(fields, "customfield_13379", "displayName");
            string idhPortfolioHead = GetNested(fields, "customfield_13376", "displayName");
            string assignedPm       = GetNested(fields, "customfield_12603", "displayName");
            string demandOwner      = GetNested(fields, "customfield_13317", "displayName");
            string chief            = GetNested(fields, "customfield_14001", "displayName");
            string assignee         = GetNested(fields, "assignee", "displayName");
            string reporter         = GetNested(fields, "reporter", "displayName");

            // DemandType from custom field (not issuetype)
            string demandType       = GetNestedAny(fields, "customfield_13339", new[] { "value", "name" });

            // Date fields
            string createdStr                = Get(fields, "created");
            string updatedStr                = Get(fields, "updated");
            string targetCompletionDate      = Get(fields, "customfield_13306");
            string proposedDemandPickup      = Get(fields, "customfield_13359");
            string actualGoLive              = Get(fields, "customfield_10964");
            string proposedBaseline0End      = Get(fields, "customfield_13375");
            string proposedBaseline0Start    = Get(fields, "customfield_13374");
            string proposedBaseline0Submit   = Get(fields, "customfield_13362");

            // Classification / Department
            string primaryClassification = GetNestedAny(fields, "customfield_13307", new[] { "value", "name" });
            string classification        = primaryClassification;
            string department            = GetNestedAny(fields, "customfield_13308", new[] { "value", "name" });

            // Parse dates
            DateTime? created    = ParseDate(createdStr);
            DateTime? updated    = ParseDate(updatedStr);
            DateTime? targetD    = ParseDate(targetCompletionDate);
            DateTime? proposedD  = ParseDate(proposedDemandPickup);
            DateTime? actualGoD  = ParseDate(actualGoLive);
            DateTime? pb0End     = ParseDate(proposedBaseline0End);
            DateTime? pb0Start   = ParseDate(proposedBaseline0Start);
            DateTime? pb0Submit  = ParseDate(proposedBaseline0Submit);

            // Compute RAG from TargetCompletionDate (same logic as ASPX)
            string computedRag = ComputeRag(targetD);

            // Manager column = AssignedProjectManager (customfield_12603), matching ASPX
            string manager = assignedPm;
            // TechLead column = SmeLead (customfield_13358), matching ASPX
            string techLead = smeLead;

            cmd.Parameters["@jid"].Value      = (object)id ?? DBNull.Value;
            cmd.Parameters["@k"].Value        = key;
            cmd.Parameters["@summary"].Value  = (object)Clip(summary, 500) ?? DBNull.Value;
            cmd.Parameters["@status"].Value   = (object)status ?? DBNull.Value;
            cmd.Parameters["@priority"].Value = (object)priority ?? DBNull.Value;
            cmd.Parameters["@itype"].Value    = (object)issuetype ?? DBNull.Value;
            cmd.Parameters["@proj"].Value     = projectKey;
            cmd.Parameters["@projName"].Value = (object)Clip(string.IsNullOrEmpty(projName) ? summary : projName, 300) ?? DBNull.Value;
            cmd.Parameters["@parent"].Value   = (object)parentJid ?? DBNull.Value;
            cmd.Parameters["@plat"].Value     = (object)platform ?? DBNull.Value;
            cmd.Parameters["@pv"].Value       = (object)platformVertical ?? DBNull.Value;
            cmd.Parameters["@pn"].Value       = (object)platformName ?? DBNull.Value;
            cmd.Parameters["@sp"].Value       = (object)secPlatform ?? DBNull.Value;
            cmd.Parameters["@arag"].Value     = (object)activityRag ?? DBNull.Value;
            cmd.Parameters["@srag"].Value     = (object)scheduleRag ?? DBNull.Value;
            cmd.Parameters["@brag"].Value     = (object)budgetRag ?? DBNull.Value;
            cmd.Parameters["@raidr"].Value    = (object)raidRag ?? DBNull.Value;
            cmd.Parameters["@orag"].Value     = (object)overallRag ?? DBNull.Value;
            cmd.Parameters["@computedRag"].Value = (object)computedRag ?? DBNull.Value;
            cmd.Parameters["@chief"].Value    = (object)chief ?? DBNull.Value;
            cmd.Parameters["@mgr"].Value      = (object)manager ?? DBNull.Value;
            cmd.Parameters["@tl"].Value       = (object)techLead ?? DBNull.Value;
            cmd.Parameters["@ael"].Value      = (object)accExecLead ?? DBNull.Value;
            cmd.Parameters["@sme"].Value      = (object)smeLead ?? DBNull.Value;
            cmd.Parameters["@ae"].Value       = (object)accExec ?? DBNull.Value;
            cmd.Parameters["@assignee"].Value = (object)assignee ?? DBNull.Value;
            cmd.Parameters["@reporter"].Value = (object)reporter ?? DBNull.Value;
            cmd.Parameters["@apm"].Value      = (object)assignedPm ?? DBNull.Value;
            cmd.Parameters["@iph"].Value      = (object)idhPortfolioHead ?? DBNull.Value;
            cmd.Parameters["@downer"].Value   = (object)demandOwner ?? DBNull.Value;
            cmd.Parameters["@dtype"].Value    = (object)demandType ?? DBNull.Value;
            cmd.Parameters["@tcd"].Value      = targetD.HasValue ? (object)targetD.Value : DBNull.Value;
            cmd.Parameters["@pdp"].Value      = proposedD.HasValue ? (object)proposedD.Value : DBNull.Value;
            cmd.Parameters["@agld"].Value     = actualGoD.HasValue ? (object)actualGoD.Value : DBNull.Value;
            cmd.Parameters["@pb0e"].Value     = pb0End.HasValue ? (object)pb0End.Value : DBNull.Value;
            cmd.Parameters["@pb0s"].Value     = pb0Start.HasValue ? (object)pb0Start.Value : DBNull.Value;
            cmd.Parameters["@pb0sb"].Value    = pb0Submit.HasValue ? (object)pb0Submit.Value : DBNull.Value;
            cmd.Parameters["@pcl"].Value      = (object)primaryClassification ?? DBNull.Value;
            cmd.Parameters["@cl"].Value       = (object)classification ?? DBNull.Value;
            cmd.Parameters["@dep"].Value      = (object)department ?? DBNull.Value;
            cmd.Parameters["@cre"].Value      = created.HasValue ? (object)created.Value : DBNull.Value;
            cmd.Parameters["@upd"].Value      = updated.HasValue ? (object)updated.Value : DBNull.Value;

            object r = cmd.ExecuteScalar();
            return r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
        }

        // ---- helpers ----
        private static string ExtractParentKey(Dictionary<string, object> fields)
        {
            if (!fields.ContainsKey("issuelinks") || fields["issuelinks"] == null) return null;
            var links = fields["issuelinks"] as System.Collections.ArrayList;
            if (links == null) return null;
            foreach (Dictionary<string, object> link in links)
            {
                var type = link["type"] as Dictionary<string, object>;
                string typeName = type != null && type.ContainsKey("name") ? type["name"].ToString() : "";
                string inward = type != null && type.ContainsKey("inward") ? type["inward"].ToString() : "";
                if (typeName.IndexOf("Parent", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    inward.IndexOf("child of", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (link.ContainsKey("inwardIssue"))
                    {
                        var iss = link["inwardIssue"] as Dictionary<string, object>;
                        if (iss != null && iss.ContainsKey("key")) return iss["key"].ToString();
                    }
                }
            }
            return null;
        }

        private static string Get(Dictionary<string, object> d, string k)
        {
            return d != null && d.ContainsKey(k) && d[k] != null ? d[k].ToString() : null;
        }
        private static string GetNested(Dictionary<string, object> d, string outer, string inner)
        {
            if (d == null || !d.ContainsKey(outer) || d[outer] == null) return null;
            var inn = d[outer] as Dictionary<string, object>;
            return Get(inn, inner);
        }
        private static string GetNestedAny(Dictionary<string, object> d, string outer, string[] innerKeys)
        {
            if (d == null || !d.ContainsKey(outer) || d[outer] == null) return null;
            var inn = d[outer] as Dictionary<string, object>;
            if (inn == null) return d[outer].ToString();
            foreach (var k in innerKeys)
            {
                string v = Get(inn, k);
                if (!string.IsNullOrEmpty(v)) return v;
            }
            return null;
        }
        private static string Clip(string s, int max)
        {
            if (s == null) return null;
            return s.Length <= max ? s : s.Substring(0, max);
        }
        private static DateTime? ParseDate(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            DateTime dt;
            if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out dt))
                return dt;
            if (DateTime.TryParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out dt))
                return dt;
            return null;
        }

        /// <summary>
        /// Compute RAG from Target Completion Date (matching JiraIntegration.aspx.cs logic).
        ///   days remaining > 30  => Red
        ///   3 < days remaining <= 30 => Amber
        ///   otherwise => Green
        /// </summary>
        private static string ComputeRag(DateTime? targetDate)
        {
            if (!targetDate.HasValue) return "Green";
            int days = (int)Math.Floor((targetDate.Value.Date - DateTime.Today).TotalDays);
            if (days > 30) return "Red";
            if (days > 3) return "Amber";
            return "Green";
        }

        /// <summary>
        /// Scan fields dictionary for any key containing "platform" (fallback matching ASPX logic).
        /// </summary>
        private static string ScanForPlatform(Dictionary<string, object> fields)
        {
            if (fields == null) return null;
            foreach (var kv in fields)
            {
                if (kv.Value == null) continue;
                string k = kv.Key.ToLowerInvariant();
                if (k.Contains("platform"))
                {
                    var d = kv.Value as Dictionary<string, object>;
                    if (d != null)
                    {
                        if (d.ContainsKey("value") && d["value"] != null) return d["value"].ToString();
                        if (d.ContainsKey("name") && d["name"] != null) return d["name"].ToString();
                    }
                    var s = kv.Value as string;
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }
            return null;
        }

        private static string HttpGet(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = HttpTimeoutMs;
            req.ReadWriteTimeout = HttpTimeoutMs;
            req.KeepAlive = true;
            req.ServicePoint.Expect100Continue = false;
            req.Method = "GET";
            req.Accept = "application/json";
            if (!string.IsNullOrEmpty(JiraUser))
            {
                string token = Convert.ToBase64String(Encoding.UTF8.GetBytes(JiraUser + ":" + JiraPass));
                req.Headers["Authorization"] = "Basic " + token;
            }
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        // POST /rest/api/2/search - preferred for high-volume / heavy field lists.
        // Putting jql + fields in the JSON body avoids URL bloat and lets Jira
        // parse the field list once; combined with bigger read/write timeouts this
        // is what unblocks DIBITP beyond startAt=1000.
        private static string JiraSearch(string jql, int startAt, int maxResults, string fieldsCsv)
        {
            string url = BaseUrl.TrimEnd('/') + "/rest/api/2/search";
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.Timeout = HttpTimeoutMs;
            req.ReadWriteTimeout = HttpTimeoutMs;
            req.KeepAlive = true;
            req.ServicePoint.Expect100Continue = false;
            req.Accept = "application/json";
            req.ContentType = "application/json";
            if (!string.IsNullOrEmpty(JiraUser))
            {
                string token = Convert.ToBase64String(Encoding.UTF8.GetBytes(JiraUser + ":" + JiraPass));
                req.Headers["Authorization"] = "Basic " + token;
            }

            var sb = new StringBuilder();
            sb.Append("{\"jql\":").Append(JsonString(jql))
              .Append(",\"startAt\":").Append(startAt)
              .Append(",\"maxResults\":").Append(maxResults);
            if (!string.IsNullOrEmpty(fieldsCsv))
            {
                sb.Append(",\"fields\":[");
                string[] parts = fieldsCsv.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(JsonString(parts[i].Trim()));
                }
                sb.Append("]");
            }
            sb.Append("}");

            byte[] payload = Encoding.UTF8.GetBytes(sb.ToString());
            req.ContentLength = payload.Length;
            using (var rs = req.GetRequestStream()) { rs.Write(payload, 0, payload.Length); }

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        private static string JsonString(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b");  break;
                    case '\f': sb.Append("\\f");  break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        // ---- SQL log helpers ----
        private static int StartSyncLog(DateTime start)
        {
            try
            {
                using (var con = new SqlConnection(ConnStr))
                {
                    con.Open();
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText = "INSERT INTO JiraSyncLog(StartTime,Status) VALUES(@s,'Running'); SELECT CAST(SCOPE_IDENTITY() AS INT);";
                        cmd.Parameters.AddWithValue("@s", start);
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch { return -1; }
        }

        private static void FinishSyncLog(int id, DateTime end, string status, string exception)
        {
            if (id <= 0) return;
            try
            {
                using (var con = new SqlConnection(ConnStr))
                {
                    con.Open();
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText = @"UPDATE JiraSyncLog
                            SET EndTime=@e, Status=@st, TotalPulled=@p, Inserted=@i, Updated=@u, Failed=@f, ExceptionLog=@x
                            WHERE SyncID=@id";
                        cmd.Parameters.AddWithValue("@e", end);
                        cmd.Parameters.AddWithValue("@st", status);
                        cmd.Parameters.AddWithValue("@p", _pulled);
                        cmd.Parameters.AddWithValue("@i", _inserted);
                        cmd.Parameters.AddWithValue("@u", _updated);
                        cmd.Parameters.AddWithValue("@f", _failed);
                        cmd.Parameters.AddWithValue("@x", (object)exception ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        private static void ExecSp(string spName)
        {
            using (var con = new SqlConnection(ConnStr))
            {
                con.Open();
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandTimeout = 300;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = spName;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void ExportCsv()
        {
            string path = CsvOut;
            using (var w = new StreamWriter(path, false, Encoding.UTF8))
            {
                w.WriteLine("JiraKey,ProjectKey,ParentJiraID,Summary,Status,Priority,Platform,PlatformVertical,OverallProjectRag,Manager,TechLead,ChiefNameMapping,CreatedDate,UpdatedDate");
                using (var con = new SqlConnection(ConnStr))
                {
                    con.Open();
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText = @"SELECT JiraKey,ProjectKey,ISNULL(ParentJiraID,''),ISNULL(Summary,''),
                            ISNULL(Status,''),ISNULL(Priority,''),ISNULL(Platform,''),ISNULL(PlatformVertical,''),
                            ISNULL(OverallProjectRag,''),ISNULL(Manager,''),ISNULL(TechLead,''),ISNULL(ChiefNameMapping,''),
                            ISNULL(CONVERT(VARCHAR(19),CreatedDate,120),''),ISNULL(CONVERT(VARCHAR(19),UpdatedDate,120),'')
                            FROM JiraIssues";
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                var sb = new StringBuilder();
                                for (int i = 0; i < rdr.FieldCount; i++)
                                {
                                    if (i > 0) sb.Append(',');
                                    string v = rdr.GetValue(i).ToString().Replace("\"", "\"\"");
                                    if (v.IndexOfAny(new[] { ',', '"', '\n' }) >= 0) v = "\"" + v + "\"";
                                    sb.Append(v);
                                }
                                w.WriteLine(sb);
                            }
                        }
                    }
                }
            }
        }

        // ---- logging ----
        private static string _logDir;
        private static void EnsureLogDir()
        {
            _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(_logDir);
        }
        private static void Log(string msg)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + " " + msg;
            Console.WriteLine(line);
            try { File.AppendAllText(Path.Combine(_logDir, "sync-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log"), line + Environment.NewLine); }
            catch { }
        }
        private static void LogResponse(string proj, int startAt, string body)
        {
            try
            {
                string fn = string.Format("api-{0}-{1}-{2}.json", proj, startAt, DateTime.Now.ToString("HHmmss"));
                File.WriteAllText(Path.Combine(_logDir, fn), body);
            }
            catch { }
        }

        // ---- Req6: scenario tracking for the deliverable exec summary ----
        private static void RecordScenario(string scenario, string detail)
        {
            if (string.IsNullOrEmpty(scenario)) return;
            int n;
            _scenarioCounts.TryGetValue(scenario, out n);
            _scenarioCounts[scenario] = n + 1;
            if (_scenarioSamples.Count < 200)
            {
                _scenarioSamples.Add(string.Format("[{0:HH:mm:ss}] {1} :: {2}",
                    DateTime.Now, scenario, detail ?? string.Empty));
            }
        }

        private static void WriteExecSummary(string finalStatus)
        {
            try
            {
                if (string.IsNullOrEmpty(_logDir)) EnsureLogDir();
                string path = Path.Combine(_logDir, "console-exec-summary.log");
                var sb = new StringBuilder();
                sb.AppendLine("===============================================================");
                sb.AppendLine(" DFM.JiraSync console execution summary  (Req6)");
                sb.AppendLine(" Generated : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine(" Status    : " + finalStatus);
                sb.AppendLine(" Pulled    : " + _pulled);
                sb.AppendLine(" Inserted  : " + _inserted);
                sb.AppendLine(" Updated   : " + _updated);
                sb.AppendLine(" Failed    : " + _failed);
                sb.AppendLine(" Batches   : " + _batchCount);
                sb.AppendLine("---------------------------------------------------------------");
                sb.AppendLine(" Scenarios encountered (" + _scenarioCounts.Count + "):");
                foreach (var kv in _scenarioCounts)
                    sb.AppendLine(string.Format("   - {0,-28}  x {1}", kv.Key, kv.Value));
                sb.AppendLine("---------------------------------------------------------------");
                sb.AppendLine(" Sample events (most recent first, capped):");
                for (int i = _scenarioSamples.Count - 1; i >= 0; i--)
                    sb.AppendLine("   " + _scenarioSamples[i]);
                sb.AppendLine("===============================================================");
                File.WriteAllText(path, sb.ToString());
            }
            catch { }
        }
    }
}
