<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="HF48BulkPostponement.aspx.cs" Inherits="HF48Workflow.HF48BulkPostponement" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>HF48 Bulk Hold / Unhold (Maker / Checker)</title>
    <style type="text/css">
        body { font-family: Segoe UI, Arial, sans-serif; margin: 0; padding: 0; background-color: #ccccff; }
        .header { background-color: #003366; color: white; padding: 15px 30px; }
        .header h1 { margin: 0; font-size: 20px; }
        .header h2 { margin: 2px 0 0 0; font-size: 13px; font-weight: normal; color: #99ccff; }
        .container { padding: 20px 30px; }
        .panel { background: #e6e6fa; border: 1px solid #b0b0d6; border-radius: 4px; margin-bottom: 20px; }
        .panel-header { background-color: #6c7ae0; color: white; padding: 10px 15px; font-weight: bold; font-size: 14px; border-radius: 4px 4px 0 0; }
        .panel-header.maker  { background-color: #28a745; }
        .panel-header.checker{ background-color: #FF6600; }
        .panel-header.audit  { background-color: #003366; }
        .panel-body { padding: 15px; }
        .form-group { margin-bottom: 10px; }
        .form-group label { display: inline-block; width: 160px; font-weight: bold; font-size: 13px; color: #333; vertical-align: top; }
        .form-group input, .form-group select, .form-group textarea {
            padding: 5px 8px; border: 1px solid #ccc; border-radius: 3px; font-size: 13px;
        }
        .btn { padding: 8px 18px; border: none; border-radius: 3px; cursor: pointer; font-size: 13px; font-weight: bold; margin-right: 5px; }
        .btn-primary { background-color: #003366; color: white; }
        .btn-primary:hover { background-color: #004488; }
        .btn-success { background-color: #28a745; color: white; }
        .btn-success:hover { background-color: #218838; }
        .btn-warning { background-color: #FF6600; color: white; }
        .btn-warning:hover { background-color: #e65c00; }
        .btn-danger { background-color: #dc3545; color: white; }
        .btn-danger:hover { background-color: #c82333; }
        .btn-info { background-color: #17a2b8; color: white; }
        .btn-info:hover { background-color: #138496; }
        .btn-sm  { padding: 3px 8px; font-size: 11px; }
        .msg-success { color: #155724; background-color: #d4edda; border: 1px solid #c3e6cb; padding: 10px 15px; border-radius: 3px; margin-bottom: 10px; }
        .msg-error   { color: #721c24; background-color: #f8d7da; border: 1px solid #f5c6cb; padding: 10px 15px; border-radius: 3px; margin-bottom: 10px; }
        .gridview { width: 100%; border-collapse: collapse; font-size: 12px; }
        .gridview th { background-color: #507CD1; color: white; padding: 8px 10px; text-align: left; font-size: 12px; }
        .gridview td { padding: 6px 10px; border-bottom: 1px solid #eee; }
        .gridview tr:hover { background-color: #f5f5f5 !important; }
        /* application colour bands - cycled by GroupIndex in code-behind */
        .grp-1 { background-color: #fff3cd; }
        .grp-2 { background-color: #d1ecf1; }
        .grp-3 { background-color: #d4edda; }
        .grp-4 { background-color: #f8d7da; }
        .grp-5 { background-color: #e2d6f3; }
        .grp-6 { background-color: #ffe5cc; }

        .new-date { color: #28a745; font-weight: bold; }
        .old-date { color: #999; text-decoration: line-through; }
        .status-pill { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: bold; color: white; }
        .status-Disbursed     { background-color: #28a745; }
        .status-NotDisbursed  { background-color: #6c757d; }
        .status-Hold          { background-color: #FF6600; }
        .who-bar  { background: #fff8d8; border: 1px dashed #d8b400; padding: 8px 12px; margin-bottom: 15px; font-size: 12px; }
        .project-meta { font-size: 12px; color: #003366; margin: 6px 0 12px 0; }
        .project-meta b { color: #000; }

        /* Drag-drop zone */
        .dropzone {
            border: 2px dashed #6c7ae0; border-radius: 6px;
            padding: 16px; text-align: center; color: #6c7ae0;
            background: #f8f8ff; cursor: pointer; font-size: 13px;
        }
        .dropzone.over { background: #e0e0ff; border-color: #003366; color: #003366; }
        .filelist { margin-top: 10px; font-size: 12px; }
        .filelist .row {
            display: flex; align-items: center; padding: 4px 6px;
            border-bottom: 1px solid #eee;
        }
        .filelist .name { flex: 1; }
        .filelist .size { color: #888; margin: 0 12px; }
        .action-tabs { margin-bottom: 10px; }
        .action-tabs .tab {
            display: inline-block; padding: 6px 18px; cursor: pointer;
            background: #e0e0e0; border: 1px solid #ccc; border-bottom: none;
            border-radius: 4px 4px 0 0; margin-right: 2px; font-size: 13px;
        }
        .action-tabs .tab.active { background: #28a745; color: white; }
        .action-tabs .tab.unhold.active { background: #FF6600; color: white; }
        .action-tabs .tab.postpone.active { background: #003366; color: white; }
    </style>
</head>
<body>
    <form id="form1" runat="server" enctype="multipart/form-data">
        <div class="header">
            <h1>HF48 Bulk Postponement / Hold / Unhold</h1>
            <h2>Project-level Postponement, Hold &amp; Resume with Maker / Checker workflow</h2>
        </div>

        <div class="container">
            <asp:Panel ID="pnlMessage" runat="server" Visible="false">
                <asp:Label ID="lblMessage" runat="server" />
            </asp:Panel>

            <div class="who-bar">
                <strong>Acting As:</strong>
                <asp:TextBox ID="txtCurrentUser" runat="server" Width="180px" />
                <asp:Button ID="btnSetUser" runat="server" Text="Switch User" CssClass="btn btn-info" OnClick="btnSetUser_Click" />
                &nbsp;<em>(Production source: <code>Session["UserID"]</code>; the textbox is here to demo Maker/Checker on the same browser.)</em>
            </div>

            <!-- =============================================================== -->
            <!-- MAKER  - Postpone / Hold / Unhold                               -->
            <!-- =============================================================== -->
            <div class="panel">
                <div class="panel-header maker">Maker &mdash; Postpone / Hold / Unhold</div>
                <div class="panel-body">

                    <div class="action-tabs">
                        <asp:LinkButton ID="lnkTabPostpone" runat="server" CssClass="tab postpone active" OnClick="lnkTabPostpone_Click">Postpone / Prepone (No Hold)</asp:LinkButton>
                        <asp:LinkButton ID="lnkTabHold"     runat="server" CssClass="tab"               OnClick="lnkTabHold_Click">Hold</asp:LinkButton>
                        <asp:LinkButton ID="lnkTabUnhold"   runat="server" CssClass="tab unhold"        OnClick="lnkTabUnhold_Click">Unhold (Resume)</asp:LinkButton>
                    </div>

                    <asp:HiddenField ID="hfMode" runat="server" Value="Postpone" />

                    <!-- HOLD / POSTPONE pane (shared) -->
                    <asp:Panel ID="pnlHoldMaker" runat="server">
                        <div class="form-group">
                            <label>Project:</label>
                            <asp:DropDownList ID="ddlProject" runat="server" Width="500px"
                                AutoPostBack="True" OnSelectedIndexChanged="ddlProject_SelectedIndexChanged" />
                            <asp:Button ID="btnRefreshProjects" runat="server" Text="Refresh" CssClass="btn btn-info"
                                OnClick="btnRefreshProjects_Click" />
                        </div>

                        <div class="project-meta">
                            <b>Developer Tier:</b> <asp:Label ID="lblProjTier" runat="server" Text="-" />
                            &nbsp;&nbsp;|&nbsp;&nbsp;
                            <b>Project Status (value_tranche):</b> <asp:Label ID="lblProjTranche" runat="server" Text="-" />
                            &nbsp;&nbsp;|&nbsp;&nbsp;
                            <b>Pending Schedules:</b> <asp:Label ID="lblProjPending" runat="server" Text="0" />
                            &nbsp;<b>Held Schedules:</b> <asp:Label ID="lblProjHeld" runat="server" Text="0" />
                        </div>

                        <div class="form-group">
                            <label>App ID Filter:</label>
                            <asp:TextBox ID="txtAppRefNo" runat="server" Width="200px" placeholder="contains..." />
                            &nbsp;<label style="width:auto;">Disb Status:</label>
                            <asp:DropDownList ID="ddlDisbStatus" runat="server">
                                <asp:ListItem Text="-- All --" Value="" />
                                <asp:ListItem Text="Not Disbursed" Value="Not Disbursed" />
                                <asp:ListItem Text="Hold" Value="Hold" />
                                <asp:ListItem Text="Disbursed" Value="Disbursed" />
                            </asp:DropDownList>
                            &nbsp;<label style="width:auto;">Shift (days):</label>
                            <asp:TextBox ID="txtShiftDays" runat="server" Width="60px" Text="0" />
                            <span style="font-size:11px;color:#666;">Postpone/Prepone: positive = postpone, <strong>negative = prepone</strong> (e.g. -10) &nbsp;|&nbsp; Hold: 0 = no date shift</span>
                        </div>
                        <div class="form-group">
                            <label>From Disb. Date:</label>
                            <asp:TextBox ID="txtFromDisbDate" runat="server" Width="120px" placeholder="dd/MM/yyyy" />
                            &nbsp;<label style="width:auto;">To Disb. Date:</label>
                            <asp:TextBox ID="txtToDisbDate" runat="server" Width="120px" placeholder="dd/MM/yyyy" />
                            &nbsp;<small style="color:#666;">Only schedules within this disbursement date range will be eligible (leave blank for all)</small>
                            &nbsp;
                            <asp:Button ID="btnPreview" runat="server" Text="Preview" CssClass="btn btn-primary"
                                OnClick="btnPreview_Click" />
                        </div>

                        <h3 style="margin:15px 0 8px 0; color:#003366;">Eligible Schedules</h3>
                        <small style="color:#666;">Eligible = lead_stage in (END_OF_PROCESS, OPS_PMU, OPS_LODGMENT, OPS_LODGEMENT). Already-Disbursed rows are shown but never modified.</small>
                        <asp:GridView ID="gvPreview" runat="server" CssClass="gridview"
                            AutoGenerateColumns="False"
                            OnRowDataBound="gvPreview_RowDataBound"
                            OnRowCommand="gvPreview_RowCommand"
                            EmptyDataText="Pick a project and click Preview."
                            AlternatingRowStyle-CssClass="">
                            <Columns>
                                <asp:BoundField DataField="APP_REF_NO"     HeaderText="App Ref No" />
                                <asp:BoundField DataField="Lead_No"        HeaderText="Lead No" />
                                <asp:BoundField DataField="Customer_name"  HeaderText="Customer" />
                                <asp:BoundField DataField="lead_stage"     HeaderText="Lead Stage" />
                                <asp:TemplateField HeaderText="Inception Date">
                                    <HeaderStyle BackColor="#507CD1" ForeColor="White" />
                                    <ItemTemplate>
                                        <span style="color:#888;font-style:italic;">
                                            <%# (Eval("InceptionDisbDate") == DBNull.Value || Eval("InceptionDisbDate") == null)
                                                ? "-" : string.Format("{0:dd/MM/yyyy}", Eval("InceptionDisbDate")) %>
                                        </span>
                                    </ItemTemplate>
                                </asp:TemplateField>
                                <asp:TemplateField HeaderText="Old Disb. Date">
                                    <ItemTemplate><span class="old-date"><%# Eval("OldDisbDate", "{0:dd/MM/yyyy}") %></span></ItemTemplate>
                                </asp:TemplateField>
                                <asp:TemplateField HeaderText="New Disb. Date">
                                    <ItemTemplate><span class="new-date"><%# Eval("NewDisbDate", "{0:dd/MM/yyyy}") %></span></ItemTemplate>
                                </asp:TemplateField>
                                <asp:BoundField DataField="Disb_amount"    HeaderText="Disb Amount"    DataFormatString="{0:N2}" />
                                <asp:BoundField DataField="finance_amount" HeaderText="Finance Amount" DataFormatString="{0:N2}" />
                                <asp:TemplateField HeaderText="Disb Status">
                                    <ItemTemplate>
                                        <span class='status-pill status-<%# Eval("disb_status").ToString().Replace(" ","") %>'><%# Eval("disb_status") %></span>
                                    </ItemTemplate>
                                </asp:TemplateField>
                                <asp:BoundField DataField="MilestonePaymentPct" HeaderText="Milestone %" DataFormatString="{0:N2}" />
                                <asp:BoundField DataField="dev_tier"            HeaderText="Tier" />
                                <asp:BoundField DataField="value_tranche"       HeaderText="Project Status" />
                                <asp:TemplateField HeaderText="History">
                                    <ItemTemplate>
                                        <asp:Button ID="btnSchedHist" runat="server"
                                            Text="History" CssClass="btn btn-info btn-sm"
                                            CommandName="ViewHistory"
                                            CommandArgument='<%# Eval("APP_REF_NO") + "|" + Eval("Lead_No") %>' />
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                        <div class="form-group" style="margin-top:8px;">
                            <asp:Label ID="lblPreviewSummary" runat="server" Style="font-size:12px;color:#003366;" />
                            &nbsp;
                            <asp:Button ID="btnExportPreviewCSV" runat="server" Text="&#11015; Export to CSV"
                                CssClass="btn btn-info" style="font-size:12px;padding:4px 10px;"
                                OnClick="btnExportPreviewCSV_Click" CausesValidation="false" />
                        </div>

                        <!-- Schedule History panel (shown on History button click) -->
                        <asp:Panel ID="pnlScheduleHistory" runat="server" Visible="false" CssClass="panel" style="margin-top:10px;">
                            <div class="panel-header audit" style="display:flex;justify-content:space-between;align-items:center;">
                                <asp:Label ID="lblScheduleHistoryTitle" runat="server" />
                                <asp:Button ID="btnCloseSchedHistory" runat="server" Text="Close"
                                    CssClass="btn btn-info btn-sm" style="float:right;"
                                    OnClick="btnCloseSchedHistory_Click" CausesValidation="false" />
                            </div>
                            <div class="panel-body">
                                <asp:GridView ID="gvScheduleHistory" runat="server" CssClass="gridview"
                                    AutoGenerateColumns="False"
                                    EmptyDataText="No postponement/hold history for this schedule.">
                                    <Columns>
                                        <asp:BoundField DataField="RequestID"       HeaderText="Req #" />
                                        <asp:BoundField DataField="RequestType"     HeaderText="Type" />
                                        <asp:BoundField DataField="OldDisbDate"     HeaderText="From Date" DataFormatString="{0:dd/MM/yyyy}" />
                                        <asp:BoundField DataField="NewDisbDate"     HeaderText="To Date"   DataFormatString="{0:dd/MM/yyyy}" />
                                        <asp:BoundField DataField="ShiftDays"       HeaderText="Shift (d)" />
                                        <asp:BoundField DataField="MakerUser"       HeaderText="Maker" />
                                        <asp:BoundField DataField="CheckerUser"     HeaderText="Checker" />
                                        <asp:BoundField DataField="CheckerOn"       HeaderText="Approved" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                                        <asp:BoundField DataField="Reason"          HeaderText="Reason" />
                                    </Columns>
                                </asp:GridView>
                            </div>
                        </asp:Panel>
                    </asp:Panel>

                    <!-- UNHOLD pane -->
                    <asp:Panel ID="pnlUnholdMaker" runat="server" Visible="false">
                        <div class="form-group">
                            <label>Active Hold to Resume:</label>
                            <asp:DropDownList ID="ddlActiveHold" runat="server" Width="700px" />
                            <asp:Button ID="btnRefreshHolds" runat="server" Text="Refresh" CssClass="btn btn-info"
                                OnClick="btnRefreshHolds_Click" />
                        </div>
                        <p style="font-size:12px;color:#666;margin-left:160px;">
                            Approving an Unhold restores both the original disbursement dates and the original disb_status from the snapshot taken at Hold time.
                        </p>
                    </asp:Panel>

                    <!-- COMMON: reason + drag-drop docs + submit -->
                    <div class="form-group" style="margin-top:18px;">
                        <label>Reason:</label>
                        <asp:TextBox ID="txtReason" runat="server" Width="700px" TextMode="MultiLine" Rows="2"
                            placeholder="e.g. Project on hold pending RERA approval / Resume after handover sign-off" />
                    </div>

                    <div class="form-group">
                        <label>Supporting Documents:</label>
                        <div style="display:inline-block; vertical-align:top; width:700px;">
                            <asp:HiddenField ID="hfDraftId" runat="server" />
                            <div id="dropzone" class="dropzone">
                                Drag &amp; drop files here, or <a href="#" onclick="document.getElementById('fileInput').click();return false;">browse</a>.<br />
                                <small>Allowed: pdf, doc(x), xls(x), jpg/png/tif, txt &mdash; up to 20 MB each.</small>
                                <input id="fileInput" type="file" multiple style="display:none;" />
                            </div>
                            <div id="filelist" class="filelist"></div>
                        </div>
                    </div>

                    <div style="margin-top:10px;">
                        <asp:Button ID="btnSubmit" runat="server" Text="Submit for Approval" CssClass="btn btn-success"
                            OnClick="btnSubmit_Click"
                            OnClientClick="return confirm('Submit this request for Checker approval?');" />
                    </div>
                </div>
            </div>

            <!-- =============================================================== -->
            <!-- CHECKER - pending queue                                          -->
            <!-- =============================================================== -->
            <div class="panel">
                <div class="panel-header checker">Checker &mdash; Pending Approvals</div>
                <div class="panel-body">
                    <asp:GridView ID="gvPending" runat="server" CssClass="gridview"
                        AutoGenerateColumns="False" DataKeyNames="RequestID"
                        OnRowCommand="gvPending_RowCommand"
                        EmptyDataText="No pending requests."
                        AlternatingRowStyle-CssClass="">
                        <Columns>
                            <asp:BoundField DataField="RequestID"   HeaderText="Req #" />
                            <asp:BoundField DataField="RequestType" HeaderText="Type" />
                            <asp:BoundField DataField="Property_ProjectName"   HeaderText="Project" />
                            <asp:BoundField DataField="Property_DeveloperName" HeaderText="Developer" />
                            <asp:BoundField DataField="ShiftDays"            HeaderText="Shift (d)" />
                            <asp:BoundField DataField="AffectedAppCount"     HeaderText="Apps" />
                            <asp:BoundField DataField="AffectedRowCount"     HeaderText="Schedules" />
                            <asp:BoundField DataField="MakerUser"            HeaderText="Maker" />
                            <asp:BoundField DataField="MakerOn"              HeaderText="Submitted" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                            <asp:BoundField DataField="Reason"               HeaderText="Reason" />
                            <asp:TemplateField HeaderText="Action">
                                <ItemTemplate>
                                    <asp:Button ID="btnView" runat="server" Text="View" CssClass="btn btn-info btn-sm"
                                        CommandName="ViewRequest" CommandArgument='<%# Eval("RequestID") %>' />
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </div>

            <asp:Panel ID="pnlDetail" runat="server" Visible="false" CssClass="panel">
                <div class="panel-header checker">
                    Request #<asp:Label ID="lblReqId" runat="server" />
                    <asp:Label ID="lblReqType" runat="server" />
                    <span style="float:right;font-weight:normal;">
                        Project: <asp:Label ID="lblReqProject" runat="server" />
                        &nbsp;|&nbsp; Maker: <asp:Label ID="lblReqMaker" runat="server" />
                        &nbsp;|&nbsp; Shift: <asp:Label ID="lblReqShift" runat="server" /> days
                    </span>
                </div>
                <div class="panel-body">
                    <asp:HiddenField ID="hfSelectedRequestId"   runat="server" />
                    <asp:HiddenField ID="hfSelectedRequestType" runat="server" />

                    <div class="form-group"><label>Maker Reason:</label><asp:Label ID="lblReqReason" runat="server" /></div>

                    <div class="form-group">
                        <label>Attached Documents:</label>
                        <asp:Repeater ID="rptDocs" runat="server">
                            <HeaderTemplate><ul style="display:inline-block;margin:0;padding-left:18px;"></HeaderTemplate>
                            <ItemTemplate>
                                <li><a href='<%# ResolveUrl((string)Eval("StoredPath")) %>' target="_blank"><%# Eval("OriginalName") %></a>
                                    <small style="color:#888;"> (<%# String.Format("{0:N0}", Eval("SizeBytes")) %> bytes)</small></li>
                            </ItemTemplate>
                            <FooterTemplate></ul></FooterTemplate>
                        </asp:Repeater>
                        <asp:Label ID="lblNoDocs" runat="server" Text="(none)" Visible="false" />
                    </div>

                    <asp:GridView ID="gvDetail" runat="server" CssClass="gridview"
                        AutoGenerateColumns="False"
                        OnRowDataBound="gvDetail_RowDataBound"
                        EmptyDataText="No detail rows."
                        AlternatingRowStyle-CssClass="">
                        <Columns>
                            <asp:BoundField DataField="APP_REF_NO"   HeaderText="App Ref No" />
                            <asp:BoundField DataField="Lead_No"      HeaderText="Lead No" />
                            <asp:BoundField DataField="Customer_Name" HeaderText="Customer" />
                            <asp:TemplateField HeaderText="Old Disb. Date">
                                <ItemTemplate><span class="old-date"><%# Eval("OldDisbDate", "{0:dd/MM/yyyy}") %></span></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="New Disb. Date">
                                <ItemTemplate><span class="new-date"><%# Eval("NewDisbDate", "{0:dd/MM/yyyy}") %></span></ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="OldDisbStatus" HeaderText="Old Status" />
                            <asp:BoundField DataField="NewDisbStatus" HeaderText="New Status" />
                            <asp:BoundField DataField="Disb_Amount"   HeaderText="Amount" DataFormatString="{0:N2}" />
                            <asp:BoundField DataField="Applied"       HeaderText="Applied?" />
                        </Columns>
                    </asp:GridView>

                    <div class="form-group" style="margin-top:15px;">
                        <label>Checker Remarks:</label>
                        <asp:TextBox ID="txtCheckerRemarks" runat="server" Width="700px" TextMode="MultiLine" Rows="2" />
                    </div>
                    <div>
                        <asp:Button ID="btnApprove" runat="server" Text="Approve &amp; Apply" CssClass="btn btn-success"
                            OnClick="btnApprove_Click"
                            OnClientClick="return confirm('Approve this request?');" />
                        <asp:Button ID="btnReject" runat="server" Text="Reject" CssClass="btn btn-danger"
                            OnClick="btnReject_Click"
                            OnClientClick="return confirm('Reject this request? Schedules will NOT be changed. Remarks are required.');" />
                        <asp:Button ID="btnCloseDetail" runat="server" Text="Close" CssClass="btn btn-info"
                            OnClick="btnCloseDetail_Click" />
                    </div>
                </div>
            </asp:Panel>

            <!-- =============================================================== -->
            <!-- HISTORY                                                          -->
            <!-- =============================================================== -->
            <div class="panel">
                <div class="panel-header audit">History (Approved / Rejected / Released)</div>
                <div class="panel-body">
                    <asp:GridView ID="gvHistory" runat="server" CssClass="gridview"
                        AutoGenerateColumns="False"
                        AllowPaging="True" PageSize="15"
                        OnPageIndexChanging="gvHistory_PageIndexChanging"
                        EmptyDataText="No closed requests yet."
                        AlternatingRowStyle-CssClass="">
                        <Columns>
                            <asp:BoundField DataField="RequestID"   HeaderText="Req #" />
                            <asp:BoundField DataField="RequestType" HeaderText="Type" />
                            <asp:BoundField DataField="Property_ProjectName" HeaderText="Project" />
                            <asp:BoundField DataField="ShiftDays"          HeaderText="Shift (d)" />
                            <asp:BoundField DataField="AffectedAppCount"   HeaderText="Apps" />
                            <asp:BoundField DataField="AffectedRowCount"   HeaderText="Schedules" />
                            <asp:BoundField DataField="Status"             HeaderText="Status" />
                            <asp:BoundField DataField="MakerUser"          HeaderText="Maker" />
                            <asp:BoundField DataField="MakerOn"            HeaderText="Submitted" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                            <asp:BoundField DataField="CheckerUser"        HeaderText="Checker" />
                            <asp:BoundField DataField="CheckerOn"          HeaderText="Decided"   DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                            <asp:BoundField DataField="CheckerRemarks"     HeaderText="Checker Remarks" />
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
        </div>

<script type="text/javascript">
(function () {
    var draftId = document.getElementById('<%= hfDraftId.ClientID %>').value;
    var dropzone = document.getElementById('dropzone');
    var fileInput = document.getElementById('fileInput');
    var listEl = document.getElementById('filelist');
    var endpoint = 'BulkPostponeUploadHandler.ashx';

    function refreshList() {
        var xhr = new XMLHttpRequest();
        xhr.open('POST', endpoint + '?action=list&draftId=' + encodeURIComponent(draftId), true);
        xhr.onload = function () {
            try {
                var data = JSON.parse(xhr.responseText);
                renderList(data.files || []);
            } catch (e) {}
        };
        xhr.send();
    }

    function renderList(files) {
        listEl.innerHTML = '';
        if (!files.length) {
            listEl.innerHTML = '<div style="color:#888;">No files attached.</div>';
            return;
        }
        for (var i = 0; i < files.length; i++) {
            var f = files[i];
            var row = document.createElement('div');
            row.className = 'row';
            row.innerHTML =
                '<span class="name">' + escapeHtml(f.name) + '</span>' +
                '<span class="size">' + f.size + ' bytes</span>' +
                '<button type="button" class="btn btn-danger btn-sm" data-id="' + f.docId + '">Remove</button>';
            row.querySelector('button').addEventListener('click', function () {
                var id = this.getAttribute('data-id');
                var x = new XMLHttpRequest();
                x.open('POST', endpoint + '?action=delete&draftId=' + encodeURIComponent(draftId) + '&docId=' + id, true);
                x.onload = refreshList;
                x.send();
            });
            listEl.appendChild(row);
        }
    }

    function escapeHtml(s) {
        return (s + '').replace(/[&<>"']/g, function (m) {
            return ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'})[m];
        });
    }

    function uploadFiles(fileObjs) {
        if (!fileObjs || !fileObjs.length) return;
        var fd = new FormData();
        for (var i = 0; i < fileObjs.length; i++) fd.append('files', fileObjs[i]);
        var xhr = new XMLHttpRequest();
        xhr.open('POST', endpoint + '?action=upload&draftId=' + encodeURIComponent(draftId), true);
        xhr.onload = refreshList;
        xhr.send(fd);
    }

    if (dropzone && fileInput) {
        dropzone.addEventListener('click', function () { fileInput.click(); });
        fileInput.addEventListener('change', function () { uploadFiles(this.files); this.value = ''; });

        ['dragenter', 'dragover'].forEach(function (ev) {
            dropzone.addEventListener(ev, function (e) {
                e.preventDefault(); e.stopPropagation();
                dropzone.classList.add('over');
            });
        });
        ['dragleave', 'drop'].forEach(function (ev) {
            dropzone.addEventListener(ev, function (e) {
                e.preventDefault(); e.stopPropagation();
                dropzone.classList.remove('over');
            });
        });
        dropzone.addEventListener('drop', function (e) {
            if (e.dataTransfer && e.dataTransfer.files) uploadFiles(e.dataTransfer.files);
        });

        refreshList();
    }
})();
</script>

    </form>
</body>
</html>
