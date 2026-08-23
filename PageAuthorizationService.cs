using System;
using System.Collections.Generic;
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

        HashSet<string> configuredKeys = GetConfiguredQueryKeys();
        configuredKeys.Add("ROLE");

        Dictionary<string, string> requestQuery = ReadRequestQuery(context.Request.QueryString);
        DataSet ds = ExecuteAuthorizationProcedure(userName, normalizedRequestPath);

        if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
        {
            return false;
        }

        DataTable menuRows = ds.Tables[0];

        foreach (DataRow row in menuRows.Rows)
        {
            string menuFileName = Convert.ToString(row["MenuFileName"]);
            if (IsMenuMatch(menuFileName, normalizedRequestPath, requestQuery, configuredKeys))
            {
                return true;
            }
        }

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

    private static bool IsMenuMatch(
        string menuFileName,
        string requestPath,
        Dictionary<string, string> requestQuery,
        HashSet<string> configuredKeys)
    {
        if (string.IsNullOrWhiteSpace(menuFileName))
        {
            return false;
        }

        string[] parts = HttpUtility.UrlDecode(menuFileName).Split('?');
        string menuPath = NormalizePath(parts[0]);

        if (!string.Equals(menuPath, requestPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parts.Length == 1)
        {
            return true;
        }

        Dictionary<string, string> menuQuery = ParseQueryPairs(parts[1]);
        if (menuQuery.Count == 0)
        {
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
                return false;
            }

            if (!string.Equals(requestValue, pair.Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Menu row has query-string conditions, but none are configured.
        if (!hasConfiguredCondition)
        {
            return false;
        }

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
