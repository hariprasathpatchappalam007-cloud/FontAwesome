<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="HF48DisbursementMonitor.aspx.cs" Inherits="HF48Workflow.HF48DisbursementMonitor" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>HF48 Disbursement Monitoring &amp; Notifications</title>
    <style type="text/css">
        body { font-family: Segoe UI, Arial, sans-serif; margin: 0; padding: 0; background-color: #ccccff; }
        .header { background-color: #003366; color: white; padding: 15px 30px; }
        .header h1 { margin: 0; font-size: 20px; }
        .header h2 { margin: 2px 0 0 0; font-size: 13px; font-weight: normal; color: #99ccff; }
        .container { padding: 20px 30px; }
        .panel { background: #e6e6fa; border: 1px solid #b0b0d6; border-radius: 4px; margin-bottom: 20px; }
        .panel-header { background-color: #6c7ae0; color: white; padding: 10px 15px; font-weight: bold; font-size: 14px; border-radius: 4px 4px 0 0; }
        .panel-body { padding: 15px; }
        .form-group { margin-bottom: 10px; }
        .form-group label { display: inline-block; width: 140px; font-weight: bold; font-size: 13px; color: #333; }
        .form-group input, .form-group select { padding: 5px 8px; border: 1px solid #ccc; border-radius: 3px; font-size: 13px; }
        .btn { padding: 8px 18px; border: none; border-radius: 3px; cursor: pointer; font-size: 13px; font-weight: bold; margin-right: 5px; }
        .btn-primary { background-color: #003366; color: white; }
        .btn-primary:hover { background-color: #004488; }
        .btn-warning { background-color: #FF6600; color: white; }
        .btn-warning:hover { background-color: #e65c00; }
        .btn-success { background-color: #28a745; color: white; }
        .btn-success:hover { background-color: #218838; }
        .btn-danger { background-color: #dc3545; color: white; }
        .btn-danger:hover { background-color: #c82333; }
        .btn-info { background-color: #17a2b8; color: white; }
        .btn-info:hover { background-color: #138496; }
        .msg-success { color: #155724; background-color: #d4edda; border: 1px solid #c3e6cb; padding: 10px 15px; border-radius: 3px; margin-bottom: 10px; }
        .msg-error { color: #721c24; background-color: #f8d7da; border: 1px solid #f5c6cb; padding: 10px 15px; border-radius: 3px; margin-bottom: 10px; }
        .gridview { width: 100%; border-collapse: collapse; font-size: 12px; }
        .gridview th { background-color: #507CD1; color: white; padding: 8px 10px; text-align: left; font-size: 12px; }
        .gridview td { padding: 6px 10px; border-bottom: 1px solid #eee; }
        .gridview tr:hover { background-color: #f5f5f5; }
        .gridview tr.alt { background-color: #EFF3FB; }
        .section-title { font-size: 16px; font-weight: bold; color: #003366; margin: 15px 0 10px 0; border-bottom: 2px solid #003366; padding-bottom: 5px; }
        .action-buttons { margin-top: 10px; }
        .tab-container { margin-bottom: 15px; }
        .tab-container .tab { display: inline-block; padding: 8px 20px; cursor: pointer; background-color: #e0e0e0; border: 1px solid #ccc; border-bottom: none; border-radius: 4px 4px 0 0; margin-right: 2px; font-size: 13px; }
        .tab-container .tab.active { background-color: #003366; color: white; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="header">
            <h1>HF48 Disbursement Monitoring &amp; Notifications</h1>
            <h2>Manual Trigger Interface - Pre-Disbursement Workflow Management</h2>
        </div>

        <div class="container">
            <!-- Status Message -->
            <asp:Panel ID="pnlMessage" runat="server" Visible="false">
                <asp:Label ID="lblMessage" runat="server" />
            </asp:Panel>

            <!-- ============================================= -->
            <!-- SECTION 1: Search / Filter Disbursements -->
            <!-- ============================================= -->
            <div class="panel">
                <div class="panel-header">Search Disbursements</div>
                <div class="panel-body">
                    <div class="form-group">
                        <label>Application No:</label>
                        <asp:TextBox ID="txtAppRefNo" runat="server" Width="200px" />
                        &nbsp;&nbsp;
                        <label>Lead No:</label>
                        <asp:TextBox ID="txtLeadNo" runat="server" Width="200px" />
                    </div>
                    <div class="form-group">
                        <label>Developer Name:</label>
                        <asp:TextBox ID="txtDeveloperName" runat="server" Width="200px" />
                        &nbsp;&nbsp;
                        <label>Project Name:</label>
                        <asp:TextBox ID="txtProjectName" runat="server" Width="200px" />
                    </div>
                    <div class="form-group">
                        <label>Disb. Date From:</label>
                        <asp:TextBox ID="txtDateFrom" runat="server" Width="120px" TextMode="Date" />
                        &nbsp;&nbsp;
                        <label>Disb. Date To:</label>
                        <asp:TextBox ID="txtDateTo" runat="server" Width="120px" TextMode="Date" />
                        &nbsp;&nbsp;
                        <label>Developer Tier:</label>
                        <asp:DropDownList ID="ddlTier" runat="server" Width="130px">
                            <asp:ListItem Text="-- All --" Value="" />
                            <asp:ListItem Text="Tier 1" Value="Tier 1" />
                            <asp:ListItem Text="Tier 2" Value="Tier 2" />
                        </asp:DropDownList>
                    </div>
                    <div class="action-buttons">
                        <asp:Button ID="btnSearch" runat="server" Text="Search" CssClass="btn btn-primary" OnClick="btnSearch_Click" />
                        <asp:Button ID="btnClear" runat="server" Text="Clear" CssClass="btn btn-info" OnClick="btnClear_Click" />
                    </div>
                </div>
            </div>

            <!-- ============================================= -->
            <!-- SECTION 2: Disbursement Grid -->
            <!-- ============================================= -->
            <div class="panel">
                <div class="panel-header">Pending Disbursements (HF48)</div>
                <div class="panel-body">
                    <asp:GridView ID="gvDisbursements" runat="server" CssClass="gridview"
                        AutoGenerateColumns="False" DataKeyNames="APP_REF_NO,Lead_No,Disb_Date"
                        AllowPaging="True" PageSize="20"
                        OnPageIndexChanging="gvDisbursements_PageIndexChanging"
                        OnRowCommand="gvDisbursements_RowCommand"
                        EmptyDataText="No disbursement records found."
                        AlternatingRowStyle-CssClass="alt">
                        <Columns>
                            <asp:BoundField DataField="APP_REF_NO" HeaderText="App Ref No" />
                            <asp:BoundField DataField="Lead_No" HeaderText="Lead No" />
                            <asp:BoundField DataField="Customer_name" HeaderText="Customer" />
                            <asp:BoundField DataField="Disb_Date" HeaderText="Disb. Date" DataFormatString="{0:dd/MM/yyyy}" />
                            <asp:BoundField DataField="Disb_amount" HeaderText="Amount" DataFormatString="{0:N2}" />
                            <asp:BoundField DataField="Property_DeveloperName" HeaderText="Developer" />
                            <asp:BoundField DataField="dev_tier" HeaderText="Tier" />
                            <asp:BoundField DataField="Property_ProjectName" HeaderText="Project" />
                            <asp:BoundField DataField="value_tranche" HeaderText="Project Status" />
                            <asp:BoundField DataField="MilestonePaymentPct" HeaderText="Milestone %" DataFormatString="{0:N2}" />
                            <asp:BoundField DataField="DaysRemaining" HeaderText="Days Left" />
                            <asp:TemplateField HeaderText="Actions">
                                <ItemTemplate>
                                    <asp:Button ID="btn60Day" runat="server" Text="60-Day" CssClass="btn btn-primary" CommandName="Send60Day"
                                        CommandArgument='<%# Eval("APP_REF_NO") + "|" + Eval("Lead_No") + "|" + Eval("Disb_Date") %>'
                                        OnClientClick="return confirm('Send 60-Day notification for this disbursement?');" />
                                    <asp:Button ID="btn30Day" runat="server" Text="30-Day" CssClass="btn btn-warning" CommandName="Send30Day"
                                        CommandArgument='<%# Eval("APP_REF_NO") + "|" + Eval("Lead_No") + "|" + Eval("Disb_Date") %>'
                                        OnClientClick="return confirm('Send 30-Day notification for this disbursement?');" />
                                    <asp:Button ID="btn10Day" runat="server" Text="10-Day" CssClass="btn btn-success" CommandName="Send10Day"
                                        CommandArgument='<%# Eval("APP_REF_NO") + "|" + Eval("Lead_No") + "|" + Eval("Disb_Date") %>'
                                        OnClientClick="return confirm('Send 10-Day notification for this disbursement?');" />
                                    <asp:Button ID="btnCreateTicket" runat="server" Text="Create Ticket" CssClass="btn btn-danger" CommandName="CreateTicket"
                                        CommandArgument='<%# Eval("APP_REF_NO") + "|" + Eval("Lead_No") + "|" + Eval("Disb_Date") %>'
                                        OnClientClick="return confirm('Create a service ticket for this disbursement?');" />
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </div>

            <!-- ============================================= -->
            <!-- SECTION 3: Open Service Tickets -->
            <!-- ============================================= -->
            <div class="panel">
                <div class="panel-header">Open Service Tickets</div>
                <div class="panel-body">
                    <asp:GridView ID="gvTasks" runat="server" CssClass="gridview"
                        AutoGenerateColumns="False" DataKeyNames="TaskID"
                        OnRowCommand="gvTasks_RowCommand"
                        EmptyDataText="No open service tickets."
                        AlternatingRowStyle-CssClass="alt">
                        <Columns>
                            <asp:BoundField DataField="TaskID" HeaderText="Task ID" />
                            <asp:BoundField DataField="APP_REF_NO" HeaderText="App Ref No" />
                            <asp:BoundField DataField="Lead_No" HeaderText="Lead No" />
                            <asp:BoundField DataField="Disb_Date" HeaderText="Disb. Date" DataFormatString="{0:dd/MM/yyyy}" />
                            <asp:BoundField DataField="TaskType" HeaderText="Type" />
                            <asp:BoundField DataField="AssignedTeam" HeaderText="Assigned Team" />
                            <asp:BoundField DataField="Status" HeaderText="Status" />
                            <asp:BoundField DataField="CreatedBy" HeaderText="Created By" />
                            <asp:BoundField DataField="CreatedOn" HeaderText="Created On" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                            <asp:TemplateField HeaderText="Action">
                                <ItemTemplate>
                                    <asp:Button ID="btnCloseTicket" runat="server" Text="Close Ticket" CssClass="btn btn-danger"
                                        CommandName="CloseTicket" CommandArgument='<%# Eval("TaskID") %>'
                                        OnClientClick="return confirm('Are you sure you want to close this service ticket?');"
                                        Enabled='<%# Eval("Status").ToString() != "Closed" %>' />
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </div>

            <!-- ============================================= -->
            <!-- SECTION 4: Audit Log -->
            <!-- ============================================= -->
            <div class="panel">
                <div class="panel-header">Audit Log</div>
                <div class="panel-body">
                    <div class="form-group">
                        <label>Filter App No:</label>
                        <asp:TextBox ID="txtAuditAppNo" runat="server" Width="200px" />
                        <asp:Button ID="btnAuditSearch" runat="server" Text="Filter Audit" CssClass="btn btn-primary" OnClick="btnAuditSearch_Click" />
                        <asp:Button ID="btnAuditAll" runat="server" Text="Show All" CssClass="btn btn-info" OnClick="btnAuditAll_Click" />
                    </div>
                    <asp:GridView ID="gvAuditLog" runat="server" CssClass="gridview"
                        AutoGenerateColumns="False"
                        AllowPaging="True" PageSize="25"
                        OnPageIndexChanging="gvAuditLog_PageIndexChanging"
                        EmptyDataText="No audit records found."
                        AlternatingRowStyle-CssClass="alt">
                        <Columns>
                            <asp:BoundField DataField="AuditID" HeaderText="ID" />
                            <asp:BoundField DataField="APP_REF_NO" HeaderText="App Ref No" />
                            <asp:BoundField DataField="Lead_No" HeaderText="Lead No" />
                            <asp:BoundField DataField="Disb_Date" HeaderText="Disb. Date" DataFormatString="{0:dd/MM/yyyy}" />
                            <asp:BoundField DataField="DaysBefore" HeaderText="Days" />
                            <asp:BoundField DataField="ActionType" HeaderText="Action Type" />
                            <asp:BoundField DataField="ActionResult" HeaderText="Result" />
                            <asp:BoundField DataField="NotificationSentTo" HeaderText="Notified To" />
                            <asp:BoundField DataField="CreatedBy" HeaderText="By" />
                            <asp:BoundField DataField="CreatedOn" HeaderText="Date" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
        </div>
    </form>
</body>
</html>
