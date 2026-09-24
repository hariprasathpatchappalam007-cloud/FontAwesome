using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Application.Security
{
    public sealed class SqlInjectionValidationResult
    {
        private SqlInjectionValidationResult(bool isValid, string matchedToken, string message)
        {
            IsValid = isValid;
            MatchedToken = matchedToken;
            Message = message;
        }

        public bool IsValid { get; private set; }

        public string MatchedToken { get; private set; }

        public string Message { get; private set; }

        public static SqlInjectionValidationResult Success()
        {
            return new SqlInjectionValidationResult(true, null, null);
        }

        public static SqlInjectionValidationResult Failure(string matchedToken)
        {
            return new SqlInjectionValidationResult(
                false,
                matchedToken,
                "Restricted text was entered. Please remove reserved SQL words or symbols.");
        }
    }

    public sealed class SqlInjectionGuard
    {
        private static readonly Regex WordRegex = new Regex(@"^[a-z0-9_]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly string _connectionString;
        private readonly object _syncRoot = new object();
        private string[] _activeTokens;

        public SqlInjectionGuard(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string is required.", "connectionString");
            }

            _connectionString = connectionString;
        }

        public SqlInjectionValidationResult ValidateControl(object control)
        {
            return ValidateText(GetControlValue(control));
        }

        public SqlInjectionValidationResult ValidateText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return SqlInjectionValidationResult.Success();
            }

            string normalizedInput = input.Trim();

            foreach (string token in GetActiveTokens())
            {
                if (IsTokenMatched(normalizedInput, token))
                {
                    return SqlInjectionValidationResult.Failure(token);
                }
            }

            return SqlInjectionValidationResult.Success();
        }

        public void RefreshTokens()
        {
            lock (_syncRoot)
            {
                _activeTokens = LoadActiveTokens();
            }
        }

        private string[] GetActiveTokens()
        {
            if (_activeTokens != null)
            {
                return _activeTokens;
            }

            lock (_syncRoot)
            {
                if (_activeTokens == null)
                {
                    _activeTokens = LoadActiveTokens();
                }
            }

            return _activeTokens;
        }

        private string[] LoadActiveTokens()
        {
            List<string> tokens = new List<string>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            using (SqlCommand command = new SqlCommand("dbo.usp_FRM_GNARR_GetSqlInjectionReservedTokens", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string token = Convert.ToString(reader["Value"]);

                        if (!string.IsNullOrWhiteSpace(token))
                        {
                            tokens.Add(token.Trim());
                        }
                    }
                }
            }

            return tokens
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(token => token.Length)
                .ToArray();
        }

        private static bool IsTokenMatched(string input, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (WordRegex.IsMatch(token))
            {
                string pattern = @"(?<![a-z0-9_])" + Regex.Escape(token) + @"(?![a-z0-9_])";
                return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            return input.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetControlValue(object control)
        {
            if (control == null)
            {
                return null;
            }

            string text = control as string;

            if (text != null)
            {
                return text;
            }

            Type controlType = control.GetType();
            string[] propertyNames = { "Text", "SelectedValue", "Value", "Date", "SelectedDate" };

            foreach (string propertyName in propertyNames)
            {
                PropertyInfo property = controlType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

                if (property == null || !property.CanRead)
                {
                    continue;
                }

                object value = property.GetValue(control, null);

                if (value != null)
                {
                    return Convert.ToString(value);
                }
            }

            return Convert.ToString(control);
        }
    }
}