using System;
using System.Collections.Generic;
using System.Xml;
using Falcon_SP.Provision.Helpers;
using Falcon_SP.Provision.Models;
using Microsoft.SharePoint.Client;

namespace Falcon_SP.Provision.Provisioners
{
    /// <summary>
    /// Provisions all SharePoint Lists required by the Falcon PET application.
    /// Each list is created idempotently – if it already exists it is skipped.
    /// </summary>
    public class ListProvisioner
    {
        private readonly ClientContext _ctx;

        public ListProvisioner(ClientContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException("ctx");
            _ctx = ctx;
        }

        // =====================================================================
        //  LIST DEFINITIONS
        // =====================================================================
        private IEnumerable<ListDefinition> GetDefinitions()
        {
            return new[]
            {
                // ── PET Projects ──────────────────────────────────────────────
                new ListDefinition
                {
                    Title = "PET Projects",
                    Description = "Stores all PET project requests",
                    TemplateType = 100,
                    EnableVersioning = true,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "PETRefNo",          DisplayName = "PET Ref No",          FieldType = "Text",     Required = true,  MaxLength = 50  },
                        new FieldDefinition { InternalName = "ProjectTitle",       DisplayName = "Project Title",       FieldType = "Text",     Required = true,  MaxLength = 255 },
                        new FieldDefinition { InternalName = "ProjectType",        DisplayName = "Project Type",        FieldType = "Choice",   Choices = "CAPEX|OPEX"           },
                        new FieldDefinition { InternalName = "BudgetSource",       DisplayName = "Budget Source",       FieldType = "Lookup",   LookupList = "Budget Source"     },
                        new FieldDefinition { InternalName = "RequestedAmountAED", DisplayName = "Requested Amount AED",FieldType = "Number",   Required = true                  },
                        new FieldDefinition { InternalName = "PETStatus",          DisplayName = "Status",              FieldType = "Choice",   Required = true,
                            Choices = "Draft|Submitted|Under Review|CAPEX Approval|OPEX Approval|PET Approval|Approved|Rejected|Sent Back|Cancelled",
                            DefaultValue = "Draft"
                        },
                        new FieldDefinition { InternalName = "Requestor",          DisplayName = "Requestor",           FieldType = "User",     Required = true                  },
                        new FieldDefinition { InternalName = "Reviewer",           DisplayName = "Reviewer",            FieldType = "User"                                       },
                        new FieldDefinition { InternalName = "CAPEXApprover",      DisplayName = "CAPEX Approver",      FieldType = "User"                                       },
                        new FieldDefinition { InternalName = "OPEXApprover",       DisplayName = "OPEX Approver",       FieldType = "User"                                       },
                        new FieldDefinition { InternalName = "PETApprover",        DisplayName = "PET Approver",        FieldType = "User"                                       },
                        new FieldDefinition { InternalName = "SubmittedDate",      DisplayName = "Submitted Date",      FieldType = "DateTime"                                   },
                        new FieldDefinition { InternalName = "ApprovedDate",       DisplayName = "Approved Date",       FieldType = "DateTime"                                   },
                        new FieldDefinition { InternalName = "JIRAProjectKey",     DisplayName = "JIRA Project Key",    FieldType = "Text",     MaxLength = 50                   },
                        new FieldDefinition { InternalName = "Remarks",            DisplayName = "Remarks",             FieldType = "Note"                                       },
                        new FieldDefinition { InternalName = "Version",            DisplayName = "PET Version",         FieldType = "Number",   DefaultValue = "1"               }
                    }
                },

                // ── Project Details (JIRA-synced) ────────────────────────────
                new ListDefinition
                {
                    Title = "Project Details",
                    Description = "Project master – synchronised from JIRA by Falcon_SP.JiraSync",
                    TemplateType = 100,
                    EnableVersioning = false,
                    Fields = new List<FieldDefinition>
                    {
                        // Identity fields (Title = project display name)
                        new FieldDefinition { InternalName = "JiraKey",              DisplayName = "JIRA Key",                 FieldType = "Text",     Required = true,  MaxLength = 50  },
                        new FieldDefinition { InternalName = "JiraStatus",           DisplayName = "JIRA Status",              FieldType = "Text",     MaxLength = 100 },
                        new FieldDefinition { InternalName = "JiraPriority",         DisplayName = "Priority",                 FieldType = "Text",     MaxLength = 50  },
                        new FieldDefinition { InternalName = "IssueType",            DisplayName = "Issue Type",               FieldType = "Text",     MaxLength = 100 },
                        new FieldDefinition { InternalName = "ProjectKey",           DisplayName = "Project Key",              FieldType = "Text",     MaxLength = 20  },
                        new FieldDefinition { InternalName = "ProjectName",          DisplayName = "Project Name",             FieldType = "Text",     MaxLength = 300 },
                        new FieldDefinition { InternalName = "ParentJiraKey",        DisplayName = "Parent JIRA Key",          FieldType = "Text",     MaxLength = 50  },
                        // Platform
                        new FieldDefinition { InternalName = "Platform",             DisplayName = "Platform",                 FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "PlatformVertical",     DisplayName = "Platform Vertical",        FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "PlatformName",         DisplayName = "Platform Name",            FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "SecondaryPlatform",    DisplayName = "Secondary Platform",       FieldType = "Text",     MaxLength = 200 },
                        // RAG
                        new FieldDefinition { InternalName = "ActivityRagStatus",    DisplayName = "Activity RAG",             FieldType = "Text",     MaxLength = 50  },
                        new FieldDefinition { InternalName = "ScheduleRag",          DisplayName = "Schedule RAG",             FieldType = "Text",     MaxLength = 50  },
                        new FieldDefinition { InternalName = "BudgetRag",            DisplayName = "Budget RAG",               FieldType = "Text",     MaxLength = 50  },
                        new FieldDefinition { InternalName = "RaidRag",              DisplayName = "RAID RAG",                 FieldType = "Text",     MaxLength = 50  },
                        new FieldDefinition { InternalName = "OverallProjectRag",    DisplayName = "Overall Project RAG",      FieldType = "Text",     MaxLength = 50  },
                        new FieldDefinition { InternalName = "ProjectRAG",           DisplayName = "Project RAG (Computed)",   FieldType = "Text",     MaxLength = 50  },
                        // People
                        new FieldDefinition { InternalName = "ChiefNameMapping",     DisplayName = "Chief",                    FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "Manager",              DisplayName = "Project Manager",          FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "TechLead",             DisplayName = "Tech Lead",                FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "AccountableExecLead",  DisplayName = "Accountable Exec Lead",    FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "SmeLead",              DisplayName = "SME Lead",                 FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "AccountableExec",      DisplayName = "Accountable Exec",         FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "Assignee",             DisplayName = "Assignee",                 FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "Reporter",             DisplayName = "Reporter",                 FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "AssignedProjectMgr",   DisplayName = "Assigned Project Manager", FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "IdhPortfolioHead",     DisplayName = "IDH Portfolio Head",       FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "DemandOwner",          DisplayName = "Demand Owner",             FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "EmployeeEmail",        DisplayName = "Employee Email",           FieldType = "Text",     MaxLength = 200 },
                        // Classification
                        new FieldDefinition { InternalName = "DemandType",           DisplayName = "Demand Type",              FieldType = "Text",     MaxLength = 100 },
                        new FieldDefinition { InternalName = "PrimaryClassification",DisplayName = "Primary Classification",   FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "Classification",       DisplayName = "Classification",           FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "Department",           DisplayName = "Department",               FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "ProjectPerformingDept",DisplayName = "Project Performing Dept",  FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "ProjectSponsorDept",   DisplayName = "Project Sponsor Dept",     FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "DemandDepartment",     DisplayName = "Demand Department",        FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "RequesterDept",        DisplayName = "Requester Department",     FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "ProjectDept",          DisplayName = "Project Department",       FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "DemandSegment",        DisplayName = "Demand Segment",           FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "DemandTitle",          DisplayName = "Demand Title",             FieldType = "Text",     MaxLength = 500 },
                        new FieldDefinition { InternalName = "RegulatoryObservation",DisplayName = "Regulatory Observation",   FieldType = "Note"   },
                        // Key dates
                        new FieldDefinition { InternalName = "JiraCreated",          DisplayName = "JIRA Created Date",        FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "JiraUpdated",          DisplayName = "JIRA Updated Date",        FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "TargetCompletionDate", DisplayName = "Target Completion Date",   FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "ProposedDemandPickup", DisplayName = "Proposed Demand Pickup",   FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "ActualGoLiveDate",     DisplayName = "Actual Go-Live Date",      FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "PB0EndDate",           DisplayName = "Baseline 0 End Date",      FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "PB0StartDate",         DisplayName = "Baseline 0 Start Date",    FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "PB0SubmitDate",        DisplayName = "Baseline 0 Submit Date",   FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "BaselineStartDate",    DisplayName = "Baseline Start Date",      FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "BaselineEndDate",      DisplayName = "Baseline End Date",        FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "BL1ActualStart",       DisplayName = "BL1 Actual Start",         FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "BL0PlannedStart",      DisplayName = "BL0 Planned Start",        FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "BL0PlannedEnd",        DisplayName = "BL0 Planned End",          FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "BL0ActualEnd",         DisplayName = "BL0 Actual End",           FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "BL1ActualGoLive",      DisplayName = "BL1 Actual Go-Live",       FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "BL0ActualStart",       DisplayName = "BL0 Actual Start",         FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "BL1ActualEnd",         DisplayName = "BL1 Actual End",           FieldType = "DateTime" },
                        // Status flags
                        new FieldDefinition { InternalName = "RolloutStatus",        DisplayName = "Rollout Status",           FieldType = "Text",     MaxLength = 100 },
                        new FieldDefinition { InternalName = "EpicStatus",           DisplayName = "Epic Status",              FieldType = "Text",     MaxLength = 100 },
                        new FieldDefinition { InternalName = "BrdStatus",            DisplayName = "BRD Status",               FieldType = "Text",     MaxLength = 100 },
                        new FieldDefinition { InternalName = "ScriptStatus",         DisplayName = "Script Status",            FieldType = "Text",     MaxLength = 100 },
                        new FieldDefinition { InternalName = "StatusGrey",           DisplayName = "Status Grey",              FieldType = "Text",     MaxLength = 100 },
                        new FieldDefinition { InternalName = "StatusReason",         DisplayName = "Status Reason",            FieldType = "Text",     MaxLength = 200 },
                        new FieldDefinition { InternalName = "InitiativeStatus",     DisplayName = "Initiative Status",        FieldType = "Text",     MaxLength = 100 },
                        new FieldDefinition { InternalName = "ProjectOverallStatus", DisplayName = "Project Overall Status",   FieldType = "Text",     MaxLength = 100 },
                        new FieldDefinition { InternalName = "CbtpBrdStatus",        DisplayName = "CBTP BRD Status",          FieldType = "Text",     MaxLength = 100 },
                        new FieldDefinition { InternalName = "FsdStatus",            DisplayName = "FSD Status",               FieldType = "Text",     MaxLength = 100 },
                        // Sync metadata
                        new FieldDefinition { InternalName = "LastSyncDate",         DisplayName = "Last Sync Date",           FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "SyncStatus",           DisplayName = "Sync Status",              FieldType = "Text",     MaxLength = 50  },
                        new FieldDefinition { InternalName = "IsActive",             DisplayName = "Is Active",                FieldType = "Boolean",  DefaultValue = "1" }
                    }
                },

                // ── Project Sizing ────────────────────────────────────────────
                new ListDefinition
                {
                    Title = "Project Sizing",
                    Description = "Stores project sizing and line item details",
                    TemplateType = 100,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "PETProject",        DisplayName = "PET Project",         FieldType = "Lookup",  LookupList = "PET Projects", Required = true },
                        new FieldDefinition { InternalName = "LineItemNo",        DisplayName = "Line No",             FieldType = "Number",  Required = true               },
                        new FieldDefinition { InternalName = "Department",        DisplayName = "Department",          FieldType = "Text",    MaxLength = 100               },
                        new FieldDefinition { InternalName = "ExpHead",           DisplayName = "Exp Head",            FieldType = "Choice",  Choices = "CAPEX|OPEX"        },
                        new FieldDefinition { InternalName = "Topic",             DisplayName = "Topic",               FieldType = "Text",    MaxLength = 300               },
                        new FieldDefinition { InternalName = "VendorName",        DisplayName = "Vendor",              FieldType = "Text",    MaxLength = 300               },
                        new FieldDefinition { InternalName = "Description",       DisplayName = "Description",         FieldType = "Note",    Required = true               },
                        new FieldDefinition { InternalName = "CostType",          DisplayName = "Cost Type",           FieldType = "Text",    MaxLength = 200               },
                        new FieldDefinition { InternalName = "UnitType",          DisplayName = "Unit Type",           FieldType = "Text",    MaxLength = 100               },
                        new FieldDefinition { InternalName = "Quantity",          DisplayName = "Units",               FieldType = "Number",  Required = true               },
                        new FieldDefinition { InternalName = "UnitPrice",         DisplayName = "Unit Price",          FieldType = "Number",  Required = true               },
                        new FieldDefinition { InternalName = "Currency",          DisplayName = "Base Currency",       FieldType = "Choice",
                            Choices = "AED|USD|EUR|GBP|INR|SGD|CHF|JPY", DefaultValue = "AED"
                        },
                        new FieldDefinition { InternalName = "ExchangeRate",      DisplayName = "Exchange Rate",       FieldType = "Number",  DefaultValue = "1"            },
                        new FieldDefinition { InternalName = "FCYAmount",         DisplayName = "Amt FCY",             FieldType = "Number"                                  },
                        new FieldDefinition { InternalName = "LCYAmount",         DisplayName = "Amt AED",             FieldType = "Number"                                  },
                        new FieldDefinition { InternalName = "Contingency",       DisplayName = "Cont %",              FieldType = "Number",  DefaultValue = "0"            },
                        new FieldDefinition { InternalName = "FinalAmountLCY",    DisplayName = "Final AED",           FieldType = "Number"                                  },
                        new FieldDefinition { InternalName = "YearlyRecurrence",  DisplayName = "Yearly Recurrence",   FieldType = "Number",  DefaultValue = "1"            },
                        new FieldDefinition { InternalName = "GLNumber",          DisplayName = "GL Number",           FieldType = "Text",    MaxLength = 50                },
                        new FieldDefinition { InternalName = "Comments",          DisplayName = "Comments",            FieldType = "Note"                                    }
                    }
                },

                // ── Budget Details ────────────────────────────────────────────
                new ListDefinition
                {
                    Title = "Budget Details",
                    Description = "Stores budget source and allocation details",
                    TemplateType = 100,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "PETProject",       DisplayName = "PET Project",      FieldType = "Lookup", LookupList = "PET Projects", Required = true },
                        new FieldDefinition { InternalName = "BudgetSourceRef",  DisplayName = "Budget Source",    FieldType = "Lookup", LookupList = "Budget Source" },
                        new FieldDefinition { InternalName = "ApprovedBudget",   DisplayName = "Approved Budget",  FieldType = "Number" },
                        new FieldDefinition { InternalName = "RequestedAmount",  DisplayName = "Requested Amount", FieldType = "Number" },
                        new FieldDefinition { InternalName = "FiscalYear",       DisplayName = "Fiscal Year",      FieldType = "Text",   MaxLength = 10 },
                        new FieldDefinition { InternalName = "BudgetRemarks",    DisplayName = "Remarks",          FieldType = "Note"   }
                    }
                },

                // ── PET Workflow ──────────────────────────────────────────────
                new ListDefinition
                {
                    Title = "PET Workflow",
                    Description = "Stores workflow status and routing history",
                    TemplateType = 100,
                    EnableVersioning = true,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "PETProject",       DisplayName = "PET Project",      FieldType = "Lookup", LookupList = "PET Projects", Required = true },
                        new FieldDefinition { InternalName = "WorkflowStep",     DisplayName = "Workflow Step",    FieldType = "Number", Required = true  },
                        new FieldDefinition { InternalName = "StepName",         DisplayName = "Step Name",        FieldType = "Text",   MaxLength = 100  },
                        new FieldDefinition { InternalName = "AssignedTo",       DisplayName = "Assigned To",      FieldType = "User"                    },
                        new FieldDefinition { InternalName = "ActionTaken",      DisplayName = "Action",           FieldType = "Choice",
                            Choices = "Pending|Approved|Rejected|Sent Back|Recalled"
                        },
                        new FieldDefinition { InternalName = "ActionDate",       DisplayName = "Action Date",      FieldType = "DateTime"                },
                        new FieldDefinition { InternalName = "Comments",         DisplayName = "Comments",         FieldType = "Note"                    },
                        new FieldDefinition { InternalName = "IsCurrent",        DisplayName = "Is Current Step",  FieldType = "Boolean", DefaultValue = "1" }
                    }
                },

                // ── Approval History ──────────────────────────────────────────
                new ListDefinition
                {
                    Title = "Approval History",
                    Description = "Audit trail of all approval actions",
                    TemplateType = 100,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "PETProject",   DisplayName = "PET Project",  FieldType = "Lookup",   LookupList = "PET Projects", Required = true },
                        new FieldDefinition { InternalName = "ApprovalStep", DisplayName = "Step",         FieldType = "Text",     MaxLength = 100 },
                        new FieldDefinition { InternalName = "ActionBy",     DisplayName = "Action By",    FieldType = "User"                     },
                        new FieldDefinition { InternalName = "Action",       DisplayName = "Action",       FieldType = "Choice",
                            Choices = "Approved|Rejected|Sent Back|Recalled|Submitted"
                        },
                        new FieldDefinition { InternalName = "ActionDate",   DisplayName = "Action Date",  FieldType = "DateTime"                 },
                        new FieldDefinition { InternalName = "Comments",     DisplayName = "Comments",     FieldType = "Note"                     }
                    }
                },

                // ── CAPEX Master ──────────────────────────────────────────────
                new ListDefinition
                {
                    Title = "CAPEX Master",
                    Description = "CAPEX category master data",
                    TemplateType = 100,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "CAPEXCode",        DisplayName = "CAPEX Code",           FieldType = "Text",    Required = true, MaxLength = 100 },
                        new FieldDefinition { InternalName = "CAPEXName",        DisplayName = "CAPEX Name",           FieldType = "Text",    Required = true, MaxLength = 500 },
                        new FieldDefinition { InternalName = "Description",      DisplayName = "Description",          FieldType = "Note"   },
                        // Budget figures (sourced from CAPEX.csv / Oracle BPM sync)
                        new FieldDefinition { InternalName = "BudgetedAmount",   DisplayName = "Budget",               FieldType = "Number" },
                        new FieldDefinition { InternalName = "UtilizedAmount",   DisplayName = "Utilization",          FieldType = "Number" },
                        new FieldDefinition { InternalName = "AvailableAmount",  DisplayName = "Available Budget",     FieldType = "Number" },
                        new FieldDefinition { InternalName = "LockedAmount",     DisplayName = "Locked Amt",           FieldType = "Number" },
                        new FieldDefinition { InternalName = "BudgetAfterLocked",DisplayName = "Budget After Locked",  FieldType = "Number" },
                        new FieldDefinition { InternalName = "ClaimAmount",      DisplayName = "Claim Amt",            FieldType = "Number" },
                        new FieldDefinition { InternalName = "NetBalance",       DisplayName = "Net Balance",          FieldType = "Number" },
                        new FieldDefinition { InternalName = "GLNumbers",        DisplayName = "GL Numbers",           FieldType = "Note"   },
                        new FieldDefinition { InternalName = "LastSyncDate",     DisplayName = "Last Sync Date",       FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "IsActive",         DisplayName = "Is Active",            FieldType = "Boolean", DefaultValue = "1" }
                    }
                },

                // ── OPEX Master ───────────────────────────────────────────────
                new ListDefinition
                {
                    Title = "OPEX Master",
                    Description = "OPEX category master data",
                    TemplateType = 100,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "OPEXCode",        DisplayName = "OPEX Code",            FieldType = "Text",    Required = true, MaxLength = 100 },
                        new FieldDefinition { InternalName = "OPEXName",        DisplayName = "OPEX Name",            FieldType = "Text",    Required = true, MaxLength = 500 },
                        new FieldDefinition { InternalName = "Description",     DisplayName = "Description",          FieldType = "Note"   },
                        // Budget figures (sourced from OPEX.csv / Oracle BPM sync)
                        new FieldDefinition { InternalName = "BudgetedAmount",  DisplayName = "Budget",               FieldType = "Number" },
                        new FieldDefinition { InternalName = "UtilizedAmount",  DisplayName = "Utilization",          FieldType = "Number" },
                        new FieldDefinition { InternalName = "AvailableAmount", DisplayName = "Available Budget",     FieldType = "Number" },
                        new FieldDefinition { InternalName = "LockedAmount",    DisplayName = "Locked Amt",           FieldType = "Number" },
                        new FieldDefinition { InternalName = "BudgetAfterLocked",DisplayName = "Budget After Locked", FieldType = "Number" },
                        new FieldDefinition { InternalName = "ClaimAmount",     DisplayName = "Claim Amt",            FieldType = "Number" },
                        new FieldDefinition { InternalName = "NetBalance",      DisplayName = "Net Balance",          FieldType = "Number" },
                        new FieldDefinition { InternalName = "Contracts",       DisplayName = "Contracts",            FieldType = "Note"   },
                        new FieldDefinition { InternalName = "LastSyncDate",    DisplayName = "Last Sync Date",       FieldType = "DateTime" },
                        new FieldDefinition { InternalName = "IsActive",        DisplayName = "Is Active",            FieldType = "Boolean", DefaultValue = "1" }
                    }
                },

                // ── Vendor Master ─────────────────────────────────────────────
                new ListDefinition
                {
                    Title = "Vendor Master",
                    Description = "Approved vendor master list",
                    TemplateType = 100,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "VendorCode",    DisplayName = "Vendor Code",    FieldType = "Text",    Required = true, MaxLength = 50  },
                        new FieldDefinition { InternalName = "VendorName",    DisplayName = "Vendor Name",    FieldType = "Text",    Required = true, MaxLength = 255 },
                        new FieldDefinition { InternalName = "ContactName",   DisplayName = "Contact Name",   FieldType = "Text",    MaxLength = 100 },
                        new FieldDefinition { InternalName = "Email",         DisplayName = "Email",          FieldType = "Text",    MaxLength = 100 },
                        new FieldDefinition { InternalName = "Phone",         DisplayName = "Phone",          FieldType = "Text",    MaxLength = 50  },
                        new FieldDefinition { InternalName = "Address",       DisplayName = "Address",        FieldType = "Note"                    },
                        new FieldDefinition { InternalName = "IsActive",      DisplayName = "Is Active",      FieldType = "Boolean", DefaultValue = "1" }
                    }
                },

                // ── GL Master ─────────────────────────────────────────────────
                new ListDefinition
                {
                    Title = "GL Master",
                    Description = "General Ledger account master",
                    TemplateType = 100,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "GLNumber",      DisplayName = "GL Number",      FieldType = "Text",    Required = true, MaxLength = 50  },
                        new FieldDefinition { InternalName = "GLDescription", DisplayName = "GL Description", FieldType = "Text",    Required = true, MaxLength = 255 },
                        new FieldDefinition { InternalName = "GLType",        DisplayName = "GL Type",        FieldType = "Choice",  Choices = "CAPEX|OPEX"          },
                        new FieldDefinition { InternalName = "BudgetedAmount",DisplayName = "Budgeted Amount",FieldType = "Number"                                   },
                        new FieldDefinition { InternalName = "IsActive",      DisplayName = "Is Active",      FieldType = "Boolean", DefaultValue = "1"              }
                    }
                },

                // ── Budget Source ─────────────────────────────────────────────
                new ListDefinition
                {
                    Title = "Budget Source",
                    Description = "Budget source master data",
                    TemplateType = 100,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "SourceCode",   DisplayName = "Source Code",   FieldType = "Text",    Required = true, MaxLength = 50  },
                        new FieldDefinition { InternalName = "SourceName",   DisplayName = "Source Name",   FieldType = "Text",    Required = true, MaxLength = 255 },
                        new FieldDefinition { InternalName = "FiscalYear",   DisplayName = "Fiscal Year",   FieldType = "Text",    MaxLength = 10 },
                        new FieldDefinition { InternalName = "TotalBudget",  DisplayName = "Total Budget",  FieldType = "Number"                  },
                        new FieldDefinition { InternalName = "IsActive",     DisplayName = "Is Active",     FieldType = "Boolean", DefaultValue = "1" }
                    }
                },

                // ── Role Management ───────────────────────────────────────────
                new ListDefinition
                {
                    Title = "Role Management",
                    Description = "Application roles and user assignments",
                    TemplateType = 100,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "UserRef",      DisplayName = "User",           FieldType = "User",    Required = true  },
                        new FieldDefinition { InternalName = "AppRole",      DisplayName = "Application Role",FieldType = "Choice", Required = true,
                            Choices = "Administrator|PET Approver|CAPEX Approver|OPEX Approver|Reviewer|Requestor"
                        },
                        new FieldDefinition { InternalName = "Department",   DisplayName = "Department",     FieldType = "Text",    MaxLength = 100 },
                        new FieldDefinition { InternalName = "IsActive",     DisplayName = "Is Active",      FieldType = "Boolean", DefaultValue = "1" },
                        new FieldDefinition { InternalName = "ValidFrom",    DisplayName = "Valid From",     FieldType = "DateTime"                   },
                        new FieldDefinition { InternalName = "ValidTo",      DisplayName = "Valid To",       FieldType = "DateTime"                   }
                    }
                },

                // ── Role Permissions ──────────────────────────────────────────
                new ListDefinition
                {
                    Title = "Role Permissions",
                    Description = "Menu and module access per role",
                    TemplateType = 100,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "RoleName",     DisplayName = "Role",       FieldType = "Choice",
                            Choices = "Administrator|PET Approver|CAPEX Approver|OPEX Approver|Reviewer|Requestor"
                        },
                        new FieldDefinition { InternalName = "ModuleName",   DisplayName = "Module",     FieldType = "Text",    MaxLength = 100 },
                        new FieldDefinition { InternalName = "CanView",      DisplayName = "Can View",   FieldType = "Boolean", DefaultValue = "0" },
                        new FieldDefinition { InternalName = "CanCreate",    DisplayName = "Can Create", FieldType = "Boolean", DefaultValue = "0" },
                        new FieldDefinition { InternalName = "CanEdit",      DisplayName = "Can Edit",   FieldType = "Boolean", DefaultValue = "0" },
                        new FieldDefinition { InternalName = "CanDelete",    DisplayName = "Can Delete", FieldType = "Boolean", DefaultValue = "0" },
                        new FieldDefinition { InternalName = "CanApprove",   DisplayName = "Can Approve",FieldType = "Boolean", DefaultValue = "0" }
                    }
                },

                // ── Email Logs ────────────────────────────────────────────────
                new ListDefinition
                {
                    Title = "Email Logs",
                    Description = "Audit history of all emails sent by the application",
                    TemplateType = 100,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "ToAddress",    DisplayName = "To",           FieldType = "Text",     MaxLength = 500 },
                        new FieldDefinition { InternalName = "CCAddress",    DisplayName = "CC",           FieldType = "Text",     MaxLength = 500 },
                        new FieldDefinition { InternalName = "Subject",      DisplayName = "Subject",      FieldType = "Text",     Required = true, MaxLength = 255 },
                        new FieldDefinition { InternalName = "Body",         DisplayName = "Body",         FieldType = "Note"                       },
                        new FieldDefinition { InternalName = "SentDate",     DisplayName = "Sent Date",    FieldType = "DateTime"                   },
                        new FieldDefinition { InternalName = "SentStatus",   DisplayName = "Status",       FieldType = "Choice",   Choices = "Sent|Failed|Pending" },
                        new FieldDefinition { InternalName = "PETRef",       DisplayName = "PET Ref",      FieldType = "Lookup",   LookupList = "PET Projects" },
                        new FieldDefinition { InternalName = "ErrorMessage", DisplayName = "Error Message",FieldType = "Note"                       }
                    }
                },

                // ── JIRA Sync Logs ────────────────────────────────────────────
                new ListDefinition
                {
                    Title = "JIRA Sync Logs",
                    Description = "JIRA synchronization history and status",
                    TemplateType = 100,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "SyncDate",      DisplayName = "Sync Date",      FieldType = "DateTime"                   },
                        new FieldDefinition { InternalName = "SyncType",      DisplayName = "Sync Type",      FieldType = "Choice",   Choices = "Full|Incremental" },
                        new FieldDefinition { InternalName = "RecordsTotal",  DisplayName = "Total Records",  FieldType = "Number"                     },
                        new FieldDefinition { InternalName = "RecordsNew",    DisplayName = "New",            FieldType = "Number"                     },
                        new FieldDefinition { InternalName = "RecordsUpdated",DisplayName = "Updated",        FieldType = "Number"                     },
                        new FieldDefinition { InternalName = "RecordsFailed", DisplayName = "Failed",         FieldType = "Number"                     },
                        new FieldDefinition { InternalName = "SyncStatus",    DisplayName = "Status",         FieldType = "Choice",   Choices = "Success|Partial|Failed" },
                        new FieldDefinition { InternalName = "ErrorDetails",  DisplayName = "Error Details",  FieldType = "Note"                       },
                        new FieldDefinition { InternalName = "TriggeredBy",   DisplayName = "Triggered By",   FieldType = "User"                       }
                    }
                },

                // ── Notifications ─────────────────────────────────────────────
                new ListDefinition
                {
                    Title = "Notifications",
                    Description = "In-app user notifications",
                    TemplateType = 100,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "NotifyUser",    DisplayName = "Notify User",    FieldType = "User",    Required = true  },
                        new FieldDefinition { InternalName = "Message",       DisplayName = "Message",        FieldType = "Note",    Required = true  },
                        new FieldDefinition { InternalName = "NotifyType",    DisplayName = "Type",           FieldType = "Choice",  Choices = "Info|Success|Warning|Error" },
                        new FieldDefinition { InternalName = "IsRead",        DisplayName = "Is Read",        FieldType = "Boolean", DefaultValue = "0" },
                        new FieldDefinition { InternalName = "PETRef",        DisplayName = "PET Ref",        FieldType = "Lookup",  LookupList = "PET Projects" },
                        new FieldDefinition { InternalName = "ActionUrl",     DisplayName = "Action URL",     FieldType = "Text",    MaxLength = 500 }
                    }
                },

                // ── Application Settings ──────────────────────────────────────
                new ListDefinition
                {
                    Title = "Application Settings",
                    Description = "Global application configuration key-value store",
                    TemplateType = 100,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "SettingKey",   DisplayName = "Setting Key",   FieldType = "Text",    Required = true, MaxLength = 100 },
                        new FieldDefinition { InternalName = "SettingValue", DisplayName = "Setting Value", FieldType = "Note",    Required = true  },
                        new FieldDefinition { InternalName = "Category",     DisplayName = "Category",      FieldType = "Text",    MaxLength = 100 },
                        new FieldDefinition { InternalName = "Description",  DisplayName = "Description",   FieldType = "Note"                    },
                        new FieldDefinition { InternalName = "IsActive",     DisplayName = "Is Active",     FieldType = "Boolean", DefaultValue = "1" }
                    }
                }
            };
        }

        // =====================================================================
        //  PUBLIC ENTRY POINT
        // =====================================================================
        public void ProvisionAll()
        {
            foreach (var def in GetDefinitions())
            {
                try
                {
                    EnsureList(def);
                }
                catch (Exception ex)
                {
                    // ProvisionLogger.Error($"Failed to provision list '{def.Title}'", ex);
                    ProvisionLogger.Error("Failed to provision list '" + def.Title + "'", ex);
                }
            }
        }

        // =====================================================================
        //  PRIVATE HELPERS
        // =====================================================================
        private void EnsureList(ListDefinition def)
        {
            _ctx.Load(_ctx.Web.Lists, lists => lists.Include(l => l.Title));
            _ctx.ExecuteQuery();

            bool exists = false;
            foreach (List l in _ctx.Web.Lists)
            {
                if (l.Title.Equals(def.Title, StringComparison.OrdinalIgnoreCase))
                { exists = true; break; }
            }

            List spList;
            if (!exists)
            {
                var ci = new ListCreationInformation
                {
                    Title        = def.Title,
                    Description  = def.Description ?? string.Empty,
                    TemplateType = def.TemplateType
                };
                spList = _ctx.Web.Lists.Add(ci);
                spList.Description = def.Description ?? string.Empty;

                if (def.EnableVersioning)
                {
                    spList.EnableVersioning      = true;
                    spList.EnableMinorVersions   = def.EnableMinorVersions;
                    spList.MajorVersionLimit      = 50;
                }

                spList.Update();
                _ctx.ExecuteQuery();
                ProvisionLogger.Success("Created list: " + def.Title);
            }
            else
            {
                spList = _ctx.Web.Lists.GetByTitle(def.Title);
                _ctx.Load(spList);
                _ctx.ExecuteQuery();
                ProvisionLogger.Skip("List already exists: " + def.Title);
            }

            // ── Snapshot existing field InternalNames ONCE per list ──────────
            // Loading inside EnsureField on every iteration would issue one HTTP
            // round-trip per field AND would be invalidated by the AddFieldAsXml
            // ExecuteQuery.  Build the set here; keep it in sync after each add.
            _ctx.Load(spList.Fields, flds => flds.Include(f => f.InternalName));
            _ctx.ExecuteQuery();

            var existingFields = new System.Collections.Generic.HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (Field f in spList.Fields)
                existingFields.Add(f.InternalName);

            // Provision fields
            foreach (var f in def.Fields)
            {
                try { EnsureField(spList, f, def.Title, existingFields); }
                catch (Exception ex) { ProvisionLogger.Error("  Field '" + f.InternalName + "' - " + ex.Message); }
            }
        }

        private void EnsureField(List spList, FieldDefinition fd, string listTitle,
            System.Collections.Generic.HashSet<string> existingFields)
        {
            if (existingFields.Contains(fd.InternalName))
            {
                ProvisionLogger.Skip("  Field exists: " + listTitle + "." + fd.InternalName);
                return;
            }

            spList.Fields.AddFieldAsXml(
                BuildFieldXml(fd, spList), true, AddFieldOptions.AddFieldInternalNameHint);
            _ctx.ExecuteQuery();
            existingFields.Add(fd.InternalName); // keep snapshot in sync
            ProvisionLogger.Success("  Added field: " + listTitle + "." + fd.InternalName + " (" + fd.FieldType + ")");

            // Wire up lookup relationship after creation
            if (fd.FieldType == "Lookup" && !string.IsNullOrEmpty(fd.LookupList))
                WireLookup(spList, fd);
        }

        private string BuildFieldXml(FieldDefinition fd, List spList)
        {
            string spType = MapToSpFieldType(fd.FieldType);
            string required = fd.Required ? " Required='TRUE'" : "";
            string maxLength = (fd.FieldType == "Text" && fd.MaxLength > 0)
                ? " MaxLength='" + fd.MaxLength.ToString() + "'" : "";

            string xml = "<Field Type='" + spType + "' DisplayName='" + XmlEncode(fd.DisplayName) + "' " +
                         "Name='" + fd.InternalName + "' StaticName='" + fd.InternalName + "'" +
                         required + maxLength + ">";

            if (fd.FieldType == "Choice" && !string.IsNullOrEmpty(fd.Choices))
            {
                xml += "<CHOICES>";
                foreach (string choice in fd.Choices.Split('|'))
                    xml += "<CHOICE>" + XmlEncode(choice) + "</CHOICE>";
                xml += "</CHOICES>";
                if (!string.IsNullOrEmpty(fd.DefaultValue))
                    xml += "<Default>" + XmlEncode(fd.DefaultValue) + "</Default>";
            }

            if (fd.FieldType == "Boolean" && !string.IsNullOrEmpty(fd.DefaultValue))
                xml += "<Default>" + fd.DefaultValue + "</Default>";

            if (fd.FieldType == "Number" && !string.IsNullOrEmpty(fd.DefaultValue))
                xml += "<Default>" + fd.DefaultValue + "</Default>";

            xml += "</Field>";
            return xml;
        }

        private void WireLookup(List spList, FieldDefinition fd)
        {
            try
            {
                List targetList = _ctx.Web.Lists.GetByTitle(fd.LookupList);
                _ctx.Load(targetList, l => l.Id);
                _ctx.ExecuteQuery();

                _ctx.Load(spList.Fields, flds => flds.Include(
                    f => f.InternalName, f => f.TypeAsString));
                _ctx.ExecuteQuery();

                foreach (Field f in spList.Fields)
                {
                    if (f.InternalName == fd.InternalName && f.TypeAsString == "Lookup")
                    {
                        var lf = _ctx.CastTo<FieldLookup>(f);
                        _ctx.Load(lf);
                        _ctx.ExecuteQuery();
                        lf.LookupList  = targetList.Id.ToString("B");
                        lf.LookupField = fd.LookupField ?? "Title";
                        lf.Update();
                        _ctx.ExecuteQuery();
                        ProvisionLogger.Success("  Wired lookup: " + fd.InternalName + " -> " + fd.LookupList);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                ProvisionLogger.Warning("  Could not wire lookup " + fd.InternalName + " -> " + fd.LookupList + ": " + ex.Message);
            }
        }

        private static string MapToSpFieldType(string fieldType)
        {
            switch (fieldType)
            {
                case "Text":     return "Text";
                case "Note":     return "Note";
                case "Number":   return "Number";
                case "DateTime": return "DateTime";
                case "Choice":   return "Choice";
                case "Lookup":   return "Lookup";
                case "Boolean":  return "Boolean";
                case "User":     return "User";
                case "URL":      return "URL";
                default:         return "Text";
            }
        }

        private static string XmlEncode(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&apos;").Replace("\"", "&quot;");
        }
    }
}
