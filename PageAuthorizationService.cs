using System;
using System.Data;
using System.Data.SqlClient;
using System.Web;

public class PageAuthorizationService
{
    private readonly string _connectionString;

    public PageAuthorizationService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public bool IsAuthorizedUser()
    {
        HttpContext context = HttpContext.Current;

        if (context == null)
        {
            return false;
        }

        string userName = Convert.ToString(context.Session["USERID"]);
        if (string.IsNullOrWhiteSpace(userName))
        {
            return false;
        }

        string normalizedRequestPath = NormalizePath(context.Request.Path);
        if (string.IsNullOrEmpty(normalizedRequestPath))
        {
            return false;
        }

        string roleFromQuery = context.Request.QueryString["ROLE"];
        DataSet ds = ExecuteAuthorizationProcedure(userName, normalizedRequestPath, roleFromQuery);

        if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
        {
            return false;
        }

        object isAuthorized = ds.Tables[0].Rows[0]["IsAuthorized"];
        return isAuthorized != DBNull.Value && Convert.ToBoolean(isAuthorized);
    }

    private DataSet ExecuteAuthorizationProcedure(string userName, string requestPath, string roleFromQuery)
    {
        DataSet dataSet = new DataSet();

        using (SqlConnection conn = new SqlConnection(_connectionString))
        using (SqlCommand cmd = new SqlCommand("dbo.usp_IsUserAuthorizedForPage", conn))
        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@LoginId", userName ?? string.Empty);
            cmd.Parameters.AddWithValue("@RequestPath", requestPath ?? string.Empty);

            if (string.IsNullOrWhiteSpace(roleFromQuery))
            {
                cmd.Parameters.AddWithValue("@RoleFromQuery", DBNull.Value);
            }
            else
            {
                cmd.Parameters.AddWithValue("@RoleFromQuery", roleFromQuery);
            }

            adapter.Fill(dataSet);
        }

        return dataSet;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string normalized = HttpUtility.UrlDecode(path).Trim();

        while (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(0, normalized.Length - 1);
        }

        return normalized;
    }
}

public static class AuthorizationCaller
{
    public static void ValidateCurrentRequest(raqValidation objRaqValidation)
    {
        string con = objRaqValidation.getAppSettings("SqlConnectionString");
        PageAuthorizationService authService = new PageAuthorizationService(con);

        if (!authService.IsAuthorizedUser())
        {
            HttpContext.Current.Response.Redirect(System.Web.VirtualPathUtility.ToAbsolute("~/login.aspx"), false);
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }
    }
}
