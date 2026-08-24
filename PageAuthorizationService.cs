using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Web;

public class PageAuthorizationService
{
    private readonly string _connectionString;
    private readonly bool _traceEnabled;
    private readonly string _traceFilePath;

    public PageAuthorizationService(string connectionString)
    {
        _connectionString = connectionString;
        _traceEnabled = true;

        HttpContext context = HttpContext.Current;
        if (context != null)
        {
            _traceFilePath = context.Server.MapPath("~/App_Data/AuthTrace.log");
        }
    }

    public bool IsAuthorizedUser()
    {
        HttpContext context = HttpContext.Current;
        string requestId = Guid.NewGuid().ToString("N");

        LogTrace(requestId, "IsAuthorizedUser - Start");

        if (context == null)
        {
            LogTrace(requestId, "Context is null. Authorization denied.");
            return false;
        }

        string rawUrl = Convert.ToString(context.Request.RawUrl);
        if (!string.IsNullOrWhiteSpace(rawUrl) && rawUrl.EndsWith("/", StringComparison.Ordinal))
        {
            LogTrace(requestId, "RawUrl ends with '/'. Authorization denied for trailing slash URL.");
            return false;
        }

        string userName = Convert.ToString(context.Session["USERID"]);
        if (string.IsNullOrWhiteSpace(userName))
        {
            LogTrace(requestId, "Session USERID is empty. Authorization denied.");
            return false;
        }

        LogTrace(requestId, "UserName=" + userName);
        LogTrace(requestId, "Request.Path=" + Convert.ToString(context.Request.Path));
        LogTrace(requestId, "Request.RawUrl=" + Convert.ToString(context.Request.RawUrl));

        string normalizedRequestPath = NormalizePath(context.Request.Path);
        if (string.IsNullOrEmpty(normalizedRequestPath))
        {
            LogTrace(requestId, "Normalized request path is empty. Authorization denied.");
            return false;
        }

        LogTrace(requestId, "NormalizedRequestPath=" + normalizedRequestPath);

        HashSet<string> configuredKeys = GetConfiguredQueryKeys();
        configuredKeys.Add("ROLE");
        LogTrace(requestId, "ConfiguredKeysCount=" + configuredKeys.Count + "; Keys=" + JoinKeys(configuredKeys));

        Dictionary<string, string> requestQuery = ReadRequestQuery(context.Request.QueryString);
        LogTrace(requestId, "RequestQueryCount=" + requestQuery.Count + "; Query=" + JoinPairs(requestQuery));

        DataSet ds = ExecuteAuthorizationProcedure(userName, normalizedRequestPath);

        if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
        {
            LogTrace(requestId, "Procedure result has no rows. Authorization denied.");
            return false;
        }

        DataTable menuRows = ds.Tables[0];
        LogTrace(requestId, "Procedure rows=" + menuRows.Rows.Count);

        for (int i = 0; i < menuRows.Rows.Count; i++)
        {
            DataRow row = menuRows.Rows[i];
            string menuFileName = Convert.ToString(row["MenuFileName"]);
            string reason;

            bool matched = IsMenuMatch(menuFileName, normalizedRequestPath, requestQuery, configuredKeys, out reason);
            LogTrace(requestId, "Row=" + i + "; MenuFileName=" + menuFileName + "; Matched=" + matched + "; Reason=" + reason);

            if (matched)
            {
                LogTrace(requestId, "Authorization granted.");
                return true;
            }
        }

        LogTrace(requestId, "No menu row matched. Authorization denied.");
        return false;
    }

    private DataSet ExecuteAuthorizationProcedure(string userName, string requestPath)
    {
        DataSet dataSet = new DataSet();

        using (SqlConnection conn = new SqlConnection(_connectionString))
        using (SqlCommand cmd = new SqlCommand("dbo.usp_IsUserAuthorizedForPage", conn))
        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@LoginId", userName ?? string.Empty);
            cmd.Parameters.AddWithValue("@RequestPath", requestPath ?? string.Empty);

            adapter.Fill(dataSet);
        }

        return dataSet;
    }

    private HashSet<string> GetConfiguredQueryKeys()
    {
        HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (SqlConnection conn = new SqlConnection(_connectionString))
        using (SqlCommand cmd = new SqlCommand("SELECT Value FROM frm_gnarr WHERE Code = @Code AND Status = 'A'", conn))
        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
        {
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.AddWithValue("@Code", "Qry_str");

            DataTable dt = new DataTable();
            adapter.Fill(dt);

            foreach (DataRow row in dt.Rows)
            {
                string key = Convert.ToString(row["Value"]);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    keys.Add(key.Trim());
                }
            }
        }

        return keys;
    }

    private static Dictionary<string, string> ReadRequestQuery(System.Collections.Specialized.NameValueCollection queryString)
    {
        Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (queryString == null)
        {
            return data;
        }

        string[] allKeys = queryString.AllKeys;
        if (allKeys == null)
        {
            return data;
        }

        for (int i = 0; i < allKeys.Length; i++)
        {
            string key = allKeys[i];
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            string value = queryString[key] ?? string.Empty;
            data[key.Trim()] = value.Trim();
        }

        return data;
    }

    private bool IsMenuMatch(
        string menuFileName,
        string requestPath,
        Dictionary<string, string> requestQuery,
        HashSet<string> configuredKeys,
        out string reason)
    {
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(menuFileName))
        {
            reason = "MenuFileName is empty.";
            return false;
        }

        string[] parts = HttpUtility.UrlDecode(menuFileName).Split('?');
        string menuPath = NormalizePath(parts[0]);

        if (!IsPathMatch(menuPath, requestPath))
        {
            reason = "Path mismatch. MenuPath=" + menuPath + "; RequestPath=" + requestPath;
            return false;
        }

        if (parts.Length == 1)
        {
            reason = "Path matched and no query condition in menu.";
            return true;
        }

        Dictionary<string, string> menuQuery = ParseQueryPairs(parts[1]);
        if (menuQuery.Count == 0)
        {
            reason = "Path matched and menu query is empty after parse.";
            return true;
        }

        bool hasConfiguredCondition = false;

        foreach (KeyValuePair<string, string> pair in menuQuery)
        {
            if (!configuredKeys.Contains(pair.Key))
            {
                continue;
            }

            hasConfiguredCondition = true;

            string requestValue;
            if (!requestQuery.TryGetValue(pair.Key, out requestValue))
            {
                reason = "Configured key missing in request. Key=" + pair.Key;
                return false;
            }

            if (!string.Equals(requestValue, pair.Value, StringComparison.OrdinalIgnoreCase))
            {
                reason = "Configured key value mismatch. Key=" + pair.Key + "; RequestValue=" + requestValue + "; MenuValue=" + pair.Value;
                return false;
            }
        }

        // Menu row has query-string conditions, but none are configured.
        if (!hasConfiguredCondition)
        {
            reason = "Menu row has query conditions but none are part of configured keys.";
            return false;
        }

        reason = "Path and configured query conditions matched.";
        return true;
    }

    private static Dictionary<string, string> ParseQueryPairs(string queryPart)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(queryPart))
        {
            return result;
        }

        string[] pairs = queryPart.Split('&');

        for (int i = 0; i < pairs.Length; i++)
        {
            string pair = pairs[i];
            if (string.IsNullOrWhiteSpace(pair))
            {
                continue;
            }

            int eqIndex = pair.IndexOf('=');
            string key;
            string value;

            if (eqIndex < 0)
            {
                key = pair.Trim();
                value = string.Empty;
            }
            else
            {
                key = pair.Substring(0, eqIndex).Trim();
                value = pair.Substring(eqIndex + 1).Trim();
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            result[HttpUtility.UrlDecode(key)] = HttpUtility.UrlDecode(value);
        }

        return result;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string normalized = HttpUtility.UrlDecode(path).Trim();

        while (normalized.Length > 0 && normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(1);
        }

        while (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(0, normalized.Length - 1);
        }

        return normalized;
    }

    private static bool IsPathMatch(string menuPath, string requestPath)
    {
        if (string.IsNullOrWhiteSpace(menuPath) || string.IsNullOrWhiteSpace(requestPath))
        {
            return false;
        }

        if (string.Equals(menuPath, requestPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Handle virtual directory prefixes on server, e.g. lms_dev/WORKFLOW/Page.aspx
        // should match menu path WORKFLOW/Page.aspx.
        string normalizedMenu = NormalizePath(menuPath);
        string normalizedRequest = NormalizePath(requestPath);

        return normalizedRequest.EndsWith("/" + normalizedMenu, StringComparison.OrdinalIgnoreCase);
    }

    private void LogTrace(string requestId, string message)
    {
        if (!_traceEnabled)
        {
            return;
        }

        string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            + " | " + requestId + " | " + message;

        try
        {
            Trace.WriteLine("[Auth] " + line);
        }
        catch
        {
        }

        if (string.IsNullOrWhiteSpace(_traceFilePath))
        {
            return;
        }

        try
        {
            string dir = Path.GetDirectoryName(_traceFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.AppendAllText(_traceFilePath, line + Environment.NewLine);
        }
        catch
        {
        }
    }

    private static string JoinKeys(HashSet<string> keys)
    {
        if (keys == null || keys.Count == 0)
        {
            return string.Empty;
        }

        List<string> list = new List<string>(keys);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return string.Join(",", list.ToArray());
    }

    private static string JoinPairs(Dictionary<string, string> pairs)
    {
        if (pairs == null || pairs.Count == 0)
        {
            return string.Empty;
        }

        List<string> list = new List<string>();
        foreach (KeyValuePair<string, string> item in pairs)
        {
            list.Add(item.Key + "=" + item.Value);
        }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        return string.Join("&", list.ToArray());
    }
}

public static class AuthorizationCaller
{
    public static void ValidateCurrentRequest(raqValidation objRaqValidation)
    {
        string requestId = Guid.NewGuid().ToString("N");
        LogCallerTrace(requestId, "ValidateCurrentRequest - Start");

        string con = objRaqValidation.getAppSettings("SqlConnectionString");
        PageAuthorizationService authService = new PageAuthorizationService(con);

        bool isAuthorized = authService.IsAuthorizedUser();
        LogCallerTrace(requestId, "IsAuthorizedUser result=" + isAuthorized);

        if (!isAuthorized)
        {
            LogCallerTrace(requestId, "Not authorized. Redirecting to login.aspx now.");

            // endResponse: true aborts the thread here so no legacy page code below runs.
            HttpContext.Current.Response.Redirect(System.Web.VirtualPathUtility.ToAbsolute("~/login.aspx"), true);

            // If this line ever logs, a try/catch in the calling page swallowed the ThreadAbortException.
            LogCallerTrace(requestId, "WARNING: code after Redirect(true) still executed.");
        }
    }

    private static void LogCallerTrace(string requestId, string message)
    {
        try
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " | " + requestId + " | " + message;
            Trace.WriteLine("[Auth] " + line);

            HttpContext context = HttpContext.Current;
            if (context == null)
            {
                return;
            }

            string traceFilePath = context.Server.MapPath("~/App_Data/AuthTrace.log");
            string dir = Path.GetDirectoryName(traceFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.AppendAllText(traceFilePath, line + Environment.NewLine);
        }
        catch
        {
        }
    }
}
