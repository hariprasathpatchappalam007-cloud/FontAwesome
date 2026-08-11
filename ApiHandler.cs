using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.SessionState;
using CET.CommandCenter.Web.Data;
using CET.CommandCenter.Web.Infrastructure;
using CET.CommandCenter.Web.Integrations.Jira;
using CET.CommandCenter.Web.Security;
using CET.CommandCenter.Web.Validation;

namespace CET.CommandCenter.Web.Handlers
{
    public sealed class ApiHandler : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext context)
        {
            try
            {
                Dispatch(context);
            }
            catch (ApiException error)
            {
                JsonHttp.WriteError(context, error.StatusCode, error.Code, error.Message, error.Details);
            }
            catch (SqlException error)
            {
                context.Trace.Warn("Database", error.Message, error);
                if (error.Number == 2601 || error.Number == 2627)
                {
                    JsonHttp.WriteError(context, 409, "DUPLICATE_RECORD", "A record with the same unique code, employee ID or email already exists.");
                    return;
                }
                JsonHttp.WriteError(context, 500, "INTERNAL_ERROR", "An unexpected error occurred.");
            }
            catch (Exception error)
            {
                context.Trace.Warn("API", error.Message, error);
                JsonHttp.WriteError(context, 500, "INTERNAL_ERROR", "An unexpected error occurred.");
            }
        }

        private static void Dispatch(HttpContext context)
        {
            var path = Convert.ToString(context.Request.RequestContext.RouteData.Values["path"] ?? string.Empty).Trim('/').ToLowerInvariant();
            var method = context.Request.HttpMethod.ToUpperInvariant();
            var store = new SqlStore();
            if (path == "health" && method == "GET")
            {
                store.Scalar("SELECT 1");
                JsonHttp.Write(context, JsonHttp.Object("status", "ok", "database", "mssql", "authMode", PortalSecurity.AuthMode,
                    "timestamp", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)), 200);
                return;
            }
            if (path == "auth/me" && method == "GET")
            {
                var current = PortalSecurity.CurrentUser(context);
                JsonHttp.Write(context, JsonHttp.Object("authenticated", current != null, "user", current, "authMode", PortalSecurity.AuthMode), 200);
                return;
            }

            var user = PortalSecurity.RequireAuthenticated(context);
            var people = new PeopleRepository(store);
            var work = new WorkRepository(store);
            var squads = new SquadRepository(store);

            if (path == "dashboard/stats" && method == "GET") Return(context, people.Stats());
            else if (path == "dashboard/ownership" && method == "GET") Return(context, people.DashboardOwnership());
            else if (path == "organization" && method == "GET") Return(context, people.Organization());
            else if (path == "verticals" && method == "GET") Return(context, people.ActiveUnits());
            else if (path == "work/summary" && method == "GET") Return(context, work.WorkSummary());
            else if (path == "portfolio-managers" && method == "GET") Return(context, work.ListPortfolioManagers());
            else if (path == "platforms" && method == "GET") Return(context, work.ListPlatforms(QueryBoolean(context, "active"), null, null));
            else if (path == "platforms" && method == "POST") SavePlatform(context, user, work, null);
            else if (path == "squads/summary" && method == "GET") Return(context, squads.Summary());
            else if (path == "squads" && method == "GET") Return(context, squads.ListSquads(context.Request.QueryString["search"], QueryBoolean(context, "active"), QueryInteger(context, "platformId"), QueryInteger(context, "managerPersonId")));
            else if (path == "squads" && method == "POST") SaveSquad(context, user, squads, null);
            else if (path == "squad-roles" && method == "GET") Return(context, squads.ListRoles(QueryBoolean(context, "active")));
            else if (path == "squad-roles" && method == "POST") SaveSquadRole(context, user, squads, null);
            else if (path == "portfolios" && method == "GET") Return(context, work.ListPortfolios(context.Request.QueryString["search"], context.Request.QueryString["status"], context.Request.QueryString["health"], QueryInteger(context, "ownerPersonId"), QueryBoolean(context, "active")));
            else if (path == "portfolios" && method == "POST") SavePortfolio(context, user, work, null);
            else if (path == "demands" && method == "GET") Return(context, work.ListDemands(context.Request.QueryString["search"], context.Request.QueryString["status"], context.Request.QueryString["health"], context.Request.QueryString["priority"], QueryInteger(context, "portfolioId"), QueryInteger(context, "ownerPersonId"), QueryBoolean(context, "active")));
            else if (path == "demands" && method == "POST") SaveDemand(context, user, work, null);
            else if (path == "people" && method == "GET") Return(context, people.List(context.Request.QueryString["search"], QueryInteger(context, "verticalId"), context.Request.QueryString["employmentType"], QueryBoolean(context, "active") ?? true));
            else if (path == "people" && method == "POST") SavePerson(context, user, people, null);
            else if (path == "units" && method == "GET") Return(context, work.ListUnits(QueryBoolean(context, "active")));
            else if (path == "units" && method == "POST") SaveUnit(context, user, work, null);
            else if (path == "uploads/profile-photo" && method == "POST") UploadProfilePhoto(context, user);
            else if (path == "integrations/jira/status" && method == "GET") Return(context, JiraIntegration.Current.GetStatus());
            else if (DispatchEntityRoute(context, user, method, path, people, work, squads)) return;
            else JsonHttp.WriteError(context, 404, "NOT_FOUND", "API endpoint not found.");
        }

        private static bool DispatchEntityRoute(HttpContext context, IDictionary<string, object> user, string method, string path,
            PeopleRepository people, WorkRepository work, SquadRepository squads)
        {
            int id;
            if (TryId(path, "portfolio-managers", out id) && method == "GET")
            {
                ReturnOrNotFound(context, work.GetPortfolioManager(id), "Portfolio manager not found.");
                return true;
            }
            if (TryId(path, "platforms", out id))
            {
                if (method == "GET") ReturnOrNotFound(context, work.GetPlatform(id), "Platform not found.");
                else if (method == "PUT") SavePlatform(context, user, work, id);
                else return false;
                return true;
            }
            if (TryId(path, "squads", out id))
            {
                if (method == "GET") ReturnOrNotFound(context, squads.GetSquad(id), "Squad not found.");
                else if (method == "PUT") SaveSquad(context, user, squads, id);
                else if (method == "DELETE")
                {
                    PortalSecurity.RequireEditor(user);
                    ReturnOrNotFound(context, squads.ArchiveSquad(id, UserEmail(user)), "Squad not found.");
                }
                else return false;
                return true;
            }
            if (TryId(path, "squad-roles", out id))
            {
                if (method == "PUT") SaveSquadRole(context, user, squads, id);
                else if (method == "DELETE")
                {
                    PortalSecurity.RequireEditor(user);
                    ReturnOrNotFound(context, squads.ArchiveRole(id, UserEmail(user)), "Squad role not found.");
                }
                else return false;
                return true;
            }
            if (TryId(path, "portfolios", out id))
            {
                if (method == "GET") ReturnOrNotFound(context, work.GetPortfolio(id), "Portfolio not found.");
                else if (method == "PUT") SavePortfolio(context, user, work, id);
                else return false;
                return true;
            }
            if (TryId(path, "demands", out id))
            {
                if (method == "GET") ReturnOrNotFound(context, work.GetDemand(id), "Demand not found.");
                else if (method == "PUT") SaveDemand(context, user, work, id);
                else return false;
                return true;
            }
            if (TryId(path, "people", out id))
            {
                if (method == "GET") ReturnOrNotFound(context, people.GetById(id), "Person not found.");
                else if (method == "PUT") SavePerson(context, user, people, id);
                else return false;
                return true;
            }
            if (TryId(path, "units", out id))
            {
                if (method == "GET") ReturnOrNotFound(context, work.GetUnit(id), "Unit not found.");
                else if (method == "PUT") SaveUnit(context, user, work, id);
                else return false;
                return true;
            }
            return false;
        }

        private static void SavePerson(HttpContext context, IDictionary<string, object> user, PeopleRepository repository, int? id)
        {
            PortalSecurity.RequireEditor(user);
            var payload = JsonHttp.ReadObject(context.Request);
            if (id.HasValue) payload["id"] = id.Value;
            Validate(RequestValidator.Person(payload, id.HasValue));
            var result = id.HasValue ? repository.UpdatePerson(id.Value, payload, UserEmail(user)) : repository.Create(payload, UserEmail(user));
            ReturnSaved(context, result, id.HasValue, "Person not found.");
        }

        private static void SaveUnit(HttpContext context, IDictionary<string, object> user, WorkRepository repository, int? id)
        {
            PortalSecurity.RequireEditor(user);
            var payload = JsonHttp.ReadObject(context.Request);
            Validate(RequestValidator.Unit(payload));
            ReturnSaved(context, repository.SaveUnit(id, payload, UserEmail(user)), id.HasValue, "Unit not found.");
        }

        private static void SavePlatform(HttpContext context, IDictionary<string, object> user, WorkRepository repository, int? id)
        {
            PortalSecurity.RequireEditor(user);
            var payload = JsonHttp.ReadObject(context.Request);
            Validate(RequestValidator.Platform(payload));
            var people = JsonHttp.IntegerList(payload, "support_person_ids").ToList();
            var owner = JsonHttp.Integer(payload, "owner_person_id");
            if (owner.HasValue) people.Add(owner.Value);
            var managerId = JsonHttp.Integer(payload, "manager_context_id") ?? 0;
            if (!repository.PlatformAssignmentsWithinManager(managerId, people))
            {
                Validate(new List<IDictionary<string, object>> { JsonHttp.Object("field", "assignments", "message", "Platform responsibility must remain within the selected manager's team.") });
            }
            ReturnSaved(context, repository.SavePlatform(id, payload, UserEmail(user)), id.HasValue, "Platform not found.");
        }

        private static void SavePortfolio(HttpContext context, IDictionary<string, object> user, WorkRepository repository, int? id)
        {
            PortalSecurity.RequireEditor(user);
            var payload = JsonHttp.ReadObject(context.Request);
            Validate(RequestValidator.Portfolio(payload));
            ReturnSaved(context, repository.SavePortfolio(id, payload, UserEmail(user)), id.HasValue, "Portfolio not found.");
        }

        private static void SaveDemand(HttpContext context, IDictionary<string, object> user, WorkRepository repository, int? id)
        {
            PortalSecurity.RequireEditor(user);
            var payload = JsonHttp.ReadObject(context.Request);
            Validate(RequestValidator.Demand(payload));
            ReturnSaved(context, repository.SaveDemand(id, payload, UserEmail(user)), id.HasValue, "Demand not found.");
        }

        private static void SaveSquad(HttpContext context, IDictionary<string, object> user, SquadRepository repository, int? id)
        {
            PortalSecurity.RequireEditor(user);
            var payload = JsonHttp.ReadObject(context.Request);
            Validate(RequestValidator.Squad(payload));
            var people = JsonHttp.ObjectList(payload, "assignments").Select(row => JsonHttp.Integer(row, "person_id") ?? 0).ToList();
            if (!repository.ValidateScope(JsonHttp.Integer(payload, "manager_person_id") ?? 0, JsonHttp.Integer(payload, "platform_id") ?? 0, people))
            {
                Validate(new List<IDictionary<string, object>> { JsonHttp.Object("field", "assignments", "message", "Platform, manager and resources must belong to the same administration tree.") });
            }
            ReturnSaved(context, repository.SaveSquad(id, payload, UserEmail(user)), id.HasValue, "Squad not found.");
        }

        private static void SaveSquadRole(HttpContext context, IDictionary<string, object> user, SquadRepository repository, int? id)
        {
            PortalSecurity.RequireEditor(user);
            var payload = JsonHttp.ReadObject(context.Request);
            Validate(RequestValidator.SquadRole(payload));
            ReturnSaved(context, repository.SaveRole(id, payload, UserEmail(user)), id.HasValue, "Squad role not found.");
        }

        private static void UploadProfilePhoto(HttpContext context, IDictionary<string, object> user)
        {
            PortalSecurity.RequireEditor(user);
            var file = context.Request.Files["photo"];
            var validTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "image/jpeg", ".jpg" }, { "image/png", ".png" }, { "image/webp", ".webp" }
            };
            string extension;
            if (file == null || file.ContentLength <= 0 || !validTypes.TryGetValue(file.ContentType ?? string.Empty, out extension))
                throw new ApiException(422, "INVALID_FILE", "Choose a JPG, PNG or WebP image.");
            int maximum;
            if (!int.TryParse(ConfigurationManager.AppSettings["MaxProfilePhotoBytes"], out maximum)) maximum = 3 * 1024 * 1024;
            if (file.ContentLength > maximum) throw new ApiException(413, "FILE_TOO_LARGE", "Profile photo exceeds the configured size limit.");
            var relativeDirectory = ConfigurationManager.AppSettings["UploadsDirectory"] ?? "~/uploads";
            var directory = context.Server.MapPath(relativeDirectory);
            Directory.CreateDirectory(directory);
            var filename = "profile-" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 12) + extension;
            file.SaveAs(Path.Combine(directory, filename));
            JsonHttp.Write(context, JsonHttp.Object("url", AppPaths.Resolve(context.Request, "uploads/" + filename)), 201);
        }

        private static void Validate(IList<IDictionary<string, object>> errors)
        {
            if (errors.Count > 0) throw new ApiException(422, "VALIDATION_ERROR", "Please correct the highlighted fields.", errors);
        }

        private static void Return(HttpContext context, object result)
        {
            JsonHttp.Write(context, result, 200);
        }

        private static void ReturnOrNotFound(HttpContext context, object result, string message)
        {
            if (result == null) throw new ApiException(404, "NOT_FOUND", message);
            Return(context, result);
        }

        private static void ReturnSaved(HttpContext context, object result, bool isUpdate, string notFoundMessage)
        {
            if (result == null) throw new ApiException(404, "NOT_FOUND", notFoundMessage);
            JsonHttp.Write(context, result, isUpdate ? 200 : 201);
        }

        private static bool TryId(string path, string segment, out int id)
        {
            id = 0;
            var match = Regex.Match(path, "^" + Regex.Escape(segment) + "/([0-9]+)$", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
        }

        private static bool? QueryBoolean(HttpContext context, string key)
        {
            var value = context.Request.QueryString[key];
            return value == null ? (bool?)null : value == "true";
        }

        private static int? QueryInteger(HttpContext context, string key)
        {
            int value;
            return int.TryParse(context.Request.QueryString[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? (int?)value : null;
        }

        private static string UserEmail(IDictionary<string, object> user)
        {
            return Convert.ToString(user["email"], CultureInfo.InvariantCulture);
        }
    }
}