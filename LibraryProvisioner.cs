using System;
using System.Collections.Generic;
using Falcon_SP.Provision.Helpers;
using Falcon_SP.Provision.Models;
using Microsoft.SharePoint.Client;

namespace Falcon_SP.Provision.Provisioners
{
    /// <summary>
    /// Provisions all Document Libraries required by the Falcon PET application.
    /// Libraries:
    ///   - Project Documents  – PET-related documents (versioning on)
    ///   - PET Attachments    – Request attachments
    ///   - CSV Imports        – Uploaded CSV files for bulk import
    ///   - Email Templates    – HTML email templates
    ///   - Application Logs   – Error and application logs
    /// </summary>
    public class LibraryProvisioner
    {
        private readonly ClientContext _ctx;

        public LibraryProvisioner(ClientContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException("ctx");
            _ctx = ctx;
        }

        private IEnumerable<ListDefinition> GetDefinitions()
        {
            return new[]
            {
                new ListDefinition
                {
                    Title            = "Project Documents",
                    Description      = "Stores PET-related documents and supporting files",
                    TemplateType     = 101,   // Document Library
                    EnableVersioning = true,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "PETRef",       DisplayName = "PET Reference",   FieldType = "Lookup", LookupList = "PET Projects" },
                        new FieldDefinition { InternalName = "DocumentType", DisplayName = "Document Type",   FieldType = "Choice",
                            Choices = "Supporting Document|Quotation|Contract|Technical Spec|Other"
                        },
                        new FieldDefinition { InternalName = "UploadedBy",   DisplayName = "Uploaded By",     FieldType = "User"   },
                        new FieldDefinition { InternalName = "DocRemarks",   DisplayName = "Remarks",         FieldType = "Note"   }
                    }
                },

                new ListDefinition
                {
                    Title            = "PET Attachments",
                    Description      = "Attachments uploaded directly against PET requests",
                    TemplateType     = 101,
                    EnableVersioning = false,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "PETRef",      DisplayName = "PET Reference",  FieldType = "Lookup", LookupList = "PET Projects" },
                        new FieldDefinition { InternalName = "UploadedBy",  DisplayName = "Uploaded By",    FieldType = "User"   },
                        new FieldDefinition { InternalName = "AttachStep",  DisplayName = "Workflow Step",  FieldType = "Text",   MaxLength = 100 }
                    }
                },

                new ListDefinition
                {
                    Title            = "CSV Imports",
                    Description      = "Stores uploaded CSV files used for bulk PET import",
                    TemplateType     = 101,
                    EnableVersioning = false,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "ImportStatus",  DisplayName = "Import Status",   FieldType = "Choice",
                            Choices = "Pending|Processing|Completed|Failed", DefaultValue = "Pending"
                        },
                        new FieldDefinition { InternalName = "TotalRows",     DisplayName = "Total Rows",      FieldType = "Number"    },
                        new FieldDefinition { InternalName = "SuccessRows",   DisplayName = "Success",         FieldType = "Number"    },
                        new FieldDefinition { InternalName = "FailedRows",    DisplayName = "Failed",          FieldType = "Number"    },
                        new FieldDefinition { InternalName = "ImportErrors",  DisplayName = "Error Details",   FieldType = "Note"      },
                        new FieldDefinition { InternalName = "ImportedBy",    DisplayName = "Imported By",     FieldType = "User"      }
                    }
                },

                new ListDefinition
                {
                    Title            = "Email Templates",
                    Description      = "HTML email templates used by the workflow notification engine",
                    TemplateType     = 101,
                    EnableVersioning = true,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "TemplateName",    DisplayName = "Template Name",    FieldType = "Text",    Required = true, MaxLength = 100 },
                        new FieldDefinition { InternalName = "TemplateSubject", DisplayName = "Subject",          FieldType = "Text",    MaxLength = 255 },
                        new FieldDefinition { InternalName = "WorkflowStep",    DisplayName = "Workflow Step",    FieldType = "Text",    MaxLength = 100 },
                        new FieldDefinition { InternalName = "IsActive",        DisplayName = "Is Active",        FieldType = "Boolean", DefaultValue = "1" }
                    }
                },

                new ListDefinition
                {
                    Title            = "Application Logs",
                    Description      = "Error and diagnostic application logs",
                    TemplateType     = 101,
                    EnableVersioning = false,
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { InternalName = "LogLevel",    DisplayName = "Log Level",    FieldType = "Choice",  Choices = "DEBUG|INFO|WARN|ERROR|FATAL" },
                        new FieldDefinition { InternalName = "Module",      DisplayName = "Module",       FieldType = "Text",    MaxLength = 100 },
                        new FieldDefinition { InternalName = "Message",     DisplayName = "Message",      FieldType = "Note"                    },
                        new FieldDefinition { InternalName = "StackTrace",  DisplayName = "Stack Trace",  FieldType = "Note"                    },
                        new FieldDefinition { InternalName = "LoggedBy",    DisplayName = "Logged By",    FieldType = "User"                    }
                    }
                }
            };
        }

        public void ProvisionAll()
        {
            foreach (var def in GetDefinitions())
            {
                try   { EnsureLibrary(def); }
                catch (Exception ex) { ProvisionLogger.Error("Failed to provision library '" + def.Title + "'", ex); }
            }
        }

        private void EnsureLibrary(ListDefinition def)
        {
            _ctx.Load(_ctx.Web.Lists, lists => lists.Include(l => l.Title, l => l.BaseTemplate));
            _ctx.ExecuteQuery();

            bool exists = false;
            List spLib  = null;
            foreach (List l in _ctx.Web.Lists)
            {
                if (l.Title.Equals(def.Title, StringComparison.OrdinalIgnoreCase))
                { exists = true; break; }
            }

            if (!exists)
            {
                var ci = new ListCreationInformation
                {
                    Title        = def.Title,
                    Description  = def.Description ?? string.Empty,
                    TemplateType = 101
                };
                spLib = _ctx.Web.Lists.Add(ci);

                if (def.EnableVersioning)
                {
                    spLib.EnableVersioning    = true;
                    spLib.MajorVersionLimit   = 20;
                }

                spLib.Update();
                _ctx.ExecuteQuery();
                ProvisionLogger.Success("Created library: " + def.Title);
            }
            else
            {
                spLib = _ctx.Web.Lists.GetByTitle(def.Title);
                _ctx.Load(spLib);
                _ctx.ExecuteQuery();
                ProvisionLogger.Skip("Library already exists: " + def.Title);
            }

            // ── Snapshot existing fields ONCE per library ────────────────
            _ctx.Load(spLib.Fields, flds => flds.Include(f => f.InternalName));
            _ctx.ExecuteQuery();

            var existingLibFields = new System.Collections.Generic.HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (Field f in spLib.Fields)
                existingLibFields.Add(f.InternalName);

            foreach (var f in def.Fields)
            {
                try { EnsureLibField(spLib, f, def.Title, existingLibFields); }
                catch (Exception ex) { ProvisionLogger.Error("  Field '" + f.InternalName + "' - " + ex.Message); }
            }
        }

        private void EnsureLibField(List spLib, FieldDefinition fd, string libTitle,
            System.Collections.Generic.HashSet<string> existingFields)
        {
            if (existingFields.Contains(fd.InternalName))
            {
                ProvisionLogger.Skip("  Field exists: " + libTitle + "." + fd.InternalName);
                return;
            }

            string spType  = fd.FieldType == "Choice" ? "Choice" : fd.FieldType == "Lookup" ? "Lookup"
                           : fd.FieldType == "Boolean" ? "Boolean" : fd.FieldType == "User" ? "User"
                           : fd.FieldType == "Note" ? "Note" : fd.FieldType == "Number" ? "Number"
                           : fd.FieldType == "DateTime" ? "DateTime" : "Text";

            string required   = fd.Required ? " Required='TRUE'" : "";
            string maxLength  = (fd.FieldType == "Text") ? " MaxLength='" + fd.MaxLength.ToString() + "'" : "";
            string xml        = "<Field Type='" + spType + "' DisplayName='" + XmlEncode(fd.DisplayName) + "' " +
                                "Name='" + fd.InternalName + "' StaticName='" + fd.InternalName + "'" + required + maxLength + ">";

            if (fd.FieldType == "Choice" && !string.IsNullOrEmpty(fd.Choices))
            {
                xml += "<CHOICES>";
                foreach (string c in fd.Choices.Split('|'))
                    xml += "<CHOICE>" + XmlEncode(c) + "</CHOICE>";
                xml += "</CHOICES>";
                if (!string.IsNullOrEmpty(fd.DefaultValue))
                    xml += "<Default>" + XmlEncode(fd.DefaultValue) + "</Default>";
            }
            if (fd.FieldType == "Boolean" && !string.IsNullOrEmpty(fd.DefaultValue))
                xml += "<Default>" + fd.DefaultValue + "</Default>";

            xml += "</Field>";

            spLib.Fields.AddFieldAsXml(xml, true, AddFieldOptions.AddFieldInternalNameHint);
            _ctx.ExecuteQuery();
            existingFields.Add(fd.InternalName); // keep snapshot in sync
            ProvisionLogger.Success("  Added field: " + libTitle + "." + fd.InternalName);
        }

        private static string XmlEncode(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&apos;").Replace("\"", "&quot;");
        }
