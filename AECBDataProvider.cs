// =============================================================================
// AECBDataProvider.cs
// Purpose : Retrieve AECB response XML from Oracle DB, store metadata in
//           SQL Server XmlProfiles, and return the XML to the caller.
//
// Usage   : var provider = new AECBDataProvider();
//           string xml   = provider.GetXmlFromDatabase(emiratesId, passportNo);
//
// Requires: Oracle.ManagedDataAccess NuGet package
//           Install-Package Oracle.ManagedDataAccess -Version 12.2.1100
//
// web.config entries needed (see web.config.snippet at end of this file):
//   appSettings key="SqlConnectionString"   -> SQL Server connection string
//   appSettings key="OracleConnectionString" -> Oracle TNS connection string
// =============================================================================

using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Xml;
using Oracle.ManagedDataAccess.Client;

/// <summary>
/// Fetches AECB credit-bureau XML from Oracle AECB_NAE_DATA,
/// caches metadata into SQL Server XmlProfiles, and returns the XML.
/// </summary>
public class AECBDataProvider
{
    // -------------------------------------------------------------------------
    // Constructor / connection strings
    // -------------------------------------------------------------------------

    private readonly string _sqlConnStr;
    private readonly string _oracleConnStr;

    public AECBDataProvider()
    {
        // Read from web.config <appSettings> (do NOT hard-code credentials)
        _sqlConnStr    = ConfigurationManager.AppSettings["SqlConnectionString"];
        _oracleConnStr = ConfigurationManager.AppSettings["OracleConnectionString"];

        if (string.IsNullOrEmpty(_sqlConnStr))
            throw new ConfigurationErrorsException("AppSettings key 'SqlConnectionString' is missing.");

        if (string.IsNullOrEmpty(_oracleConnStr))
            throw new ConfigurationErrorsException("AppSettings key 'OracleConnectionString' is missing.");
    }

    // -------------------------------------------------------------------------
    // Public entry point
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the AECB XML for the supplied Emirates ID or Passport Number.
    /// The XML is sourced from Oracle; metadata is upserted to SQL Server.
    /// </summary>
    /// <param name="emiratesId">Emirates ID (dashes optional).</param>
    /// <param name="passportNo">Passport number.</param>
    /// <returns>AECB MGResponse XML string.</returns>
    public string GetXmlFromDatabase(string emiratesId, string passportNo)
    {
        string xmlData = GetXmlFromOracle(emiratesId, passportNo);

        if (!string.IsNullOrEmpty(xmlData))
        {
            // Strip any XML declaration that may cause downstream parsing issues
            xmlData = StripXmlDeclaration(xmlData);

            // Parse metadata from XML and upsert into SQL Server
            AECBMetadata meta = ParseMetadata(xmlData, emiratesId, passportNo);
            UpsertMetadataToSql(meta, xmlData);

            return xmlData;
        }

        // Record not found in Oracle – return standard error response
        return BuildNotFoundResponse();
    }

    // -------------------------------------------------------------------------
    // Oracle: fetch AECBDATA
    // -------------------------------------------------------------------------

    /// <summary>
    /// Queries Oracle SOAAECB.AECB_NAE_DATA for the most-recent matching row.
    /// ASSUMPTION: The table exposes EMIRATESID and PASSPORTNO columns for
    /// filtering. If the table structure differs, adjust the WHERE clause below.
    /// </summary>
    private string GetXmlFromOracle(string emiratesId, string passportNo)
    {
        string cleanId = (emiratesId ?? string.Empty).Replace("-", "");

        // Oracle does not support ROWNUM + ORDER BY at the same query level.
        // Wrap in a sub-query so the ORDER BY is applied before ROWNUM limits.
        const string query =
            "SELECT AECBDATA " +
            "FROM ( " +
            "    SELECT AECBDATA " +
            "    FROM   SOAAECB.AECB_NAE_DATA " +
            "    WHERE  ( REPLACE(NVL(EMIRATESID, ''), '-', '') = :emiratesId " +
            "          OR NVL(PASSPORTNO, '') = :passportNo ) " +
            "    ORDER  BY REQUESTDATE DESC " +
            ") WHERE ROWNUM = 1";

        try
        {
            using (OracleConnection con = new OracleConnection(_oracleConnStr))
            {
                con.Open();

                using (OracleCommand cmd = new OracleCommand(query, con))
                {
                    // Parameterised – prevents SQL injection
                    cmd.Parameters.Add("emiratesId", OracleDbType.Varchar2).Value =
                        cleanId.Length > 0 ? (object)cleanId : DBNull.Value;

                    cmd.Parameters.Add("passportNo", OracleDbType.Varchar2).Value =
                        !string.IsNullOrEmpty(passportNo) ? (object)passportNo : DBNull.Value;

                    object result = cmd.ExecuteScalar();

                    if (result != null && result != DBNull.Value)
                        return result.ToString();
                }
            }
        }
        catch (OracleException oEx)
        {
            throw new Exception("Oracle data access error: " + oEx.Message, oEx);
        }

        return string.Empty;
    }

    // -------------------------------------------------------------------------
    // XML parsing
    // -------------------------------------------------------------------------

    /// <summary>
    /// Extracts FullNameEN, EmiratesId, and PassportNo from the MGResponse XML.
    /// Falls back to the caller-supplied values when nodes are absent.
    /// </summary>
    private AECBMetadata ParseMetadata(string xml, string fallbackEmiratesId, string fallbackPassportNo)
    {
        var meta = new AECBMetadata
        {
            EmiratesId = fallbackEmiratesId,
            PassportNo = fallbackPassportNo,
            FullNameEN = string.Empty
        };

        try
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            XmlNamespaceManager ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("mg", "urn:crif-messagegateway:2006-08-23");
            ns.AddNamespace("u3", "urn:NAE");

            XmlNode nameNode = doc.SelectSingleNode("//u3:FullNameEN", ns);
            if (nameNode != null)
                meta.FullNameEN = nameNode.InnerText;

            XmlNode eidNode = doc.SelectSingleNode("//u3:EmiratesId", ns);
            if (eidNode != null)
                meta.EmiratesId = eidNode.InnerText;

            XmlNode ppNode = doc.SelectSingleNode("//u3:PassportNo", ns);
            if (ppNode != null)
                meta.PassportNo = ppNode.InnerText;
        }
        catch
        {
            // Use the fallback values already set above
        }

        return meta;
    }

    // -------------------------------------------------------------------------
    // SQL Server: upsert metadata
    // -------------------------------------------------------------------------

    /// <summary>
    /// Updates an existing XmlProfiles row or inserts a new one.
    /// All parameters are bound – no string concatenation in SQL.
    /// </summary>
    private void UpsertMetadataToSql(AECBMetadata meta, string xmlData)
    {
        string cleanId = (meta.EmiratesId ?? string.Empty).Replace("-", "");

        // Check whether this Emirates ID or Passport No already has a row
        const string selectSql =
            "SELECT TOP 1 ProfileId " +
            "FROM   [dbo].[XmlProfiles] " +
            "WHERE  REPLACE(ISNULL(EmiratesId, ''), '-', '') = @emiratesId " +
            "    OR ISNULL(PassportNo, '') = @passportNo";

        const string updateSql =
            "UPDATE [dbo].[XmlProfiles] " +
            "SET    FullNameEN   = @fullNameEN, " +
            "       XmlData      = @xmlData, " +
            "       ModifiedDate = @now, " +
            "       IsActive     = 1 " +
            "WHERE  ProfileId = @profileId";

        const string insertSql =
            "INSERT INTO [dbo].[XmlProfiles] " +
            "       (FullNameEN, EmiratesId, PassportNo, XmlData, SourceFile, " +
            "        CreatedDate, ModifiedDate, CreatedBy, IsActive) " +
            "VALUES (@fullNameEN, @emiratesId, @passportNo, @xmlData, @sourceFile, " +
            "        @now, @now, @createdBy, 1)";

        try
        {
            using (SqlConnection con = new SqlConnection(_sqlConnStr))
            {
                con.Open();

                // --- Check for existing record ---
                int profileId = 0;
                using (SqlCommand selCmd = new SqlCommand(selectSql, con))
                {
                    selCmd.Parameters.Add("@emiratesId", SqlDbType.NVarChar, 50).Value = cleanId;
                    selCmd.Parameters.Add("@passportNo",  SqlDbType.NVarChar, 50).Value =
                        (object)meta.PassportNo ?? DBNull.Value;

                    object res = selCmd.ExecuteScalar();
                    if (res != null && res != DBNull.Value)
                        profileId = Convert.ToInt32(res);
                }

                if (profileId > 0)
                {
                    // --- Update ---
                    using (SqlCommand updCmd = new SqlCommand(updateSql, con))
                    {
                        updCmd.Parameters.Add("@fullNameEN", SqlDbType.NVarChar, 200).Value =
                            (object)meta.FullNameEN ?? DBNull.Value;
                        updCmd.Parameters.Add("@xmlData",    SqlDbType.NVarChar, -1).Value = xmlData;
                        updCmd.Parameters.Add("@now",        SqlDbType.DateTime).Value      = DateTime.Now;
                        updCmd.Parameters.Add("@profileId",  SqlDbType.Int).Value           = profileId;
                        updCmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    // --- Insert ---
                    using (SqlCommand insCmd = new SqlCommand(insertSql, con))
                    {
                        insCmd.Parameters.Add("@fullNameEN",  SqlDbType.NVarChar, 200).Value =
                            (object)meta.FullNameEN ?? DBNull.Value;
                        insCmd.Parameters.Add("@emiratesId",  SqlDbType.NVarChar, 50).Value  =
                            (object)meta.EmiratesId ?? DBNull.Value;
                        insCmd.Parameters.Add("@passportNo",  SqlDbType.NVarChar, 50).Value  =
                            (object)meta.PassportNo ?? DBNull.Value;
                        insCmd.Parameters.Add("@xmlData",     SqlDbType.NVarChar, -1).Value  = xmlData;
                        insCmd.Parameters.Add("@sourceFile",  SqlDbType.NVarChar, 200).Value = "OracleAECB";
                        insCmd.Parameters.Add("@now",         SqlDbType.DateTime).Value       = DateTime.Now;
                        insCmd.Parameters.Add("@createdBy",   SqlDbType.NVarChar, 100).Value  = "AECBService";
                        insCmd.ExecuteNonQuery();
                    }
                }
            }
        }
        catch (SqlException sqlEx)
        {
            throw new Exception("SQL Server metadata store error: " + sqlEx.Message, sqlEx);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string StripXmlDeclaration(string xml)
    {
        // Remove common XML declarations that break XML fragment consumers
        xml = xml.Replace("<?xml version=\"1.0\" encoding=\"utf-16\"?>", "");
        xml = xml.Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?>",  "");
        xml = xml.Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>",  "");
        return xml.Trim();
    }

    private static string BuildNotFoundResponse()
    {
        return
            "<MGResponse xmlns=\"urn:crif-messagegateway:2006-08-23\"" +
            " xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
            "<MGResponse xmlns=\"urn:crif-messagegateway:2006-08-23\">" +
            "<u3:NAE_RES xmlns:u3=\"urn:NAE\">" +
            "<u3:ResponseId>a83a1345-9e92-46ee-a7f0-bb020c57a2c1</u3:ResponseId>" +
            "<u3:Error>" +
            "<u3:No>211</u3:No>" +
            "<u3:Description>EmiratesId is not valid</u3:Description>" +
            "</u3:Error>" +
            "</u3:NAE_RES>" +
            "</MGResponse>" +
            "</MGResponse>";
    }

    // -------------------------------------------------------------------------
    // Private DTO
    // -------------------------------------------------------------------------

    private class AECBMetadata
    {
        public string FullNameEN { get; set; }
        public string EmiratesId { get; set; }
        public string PassportNo { get; set; }
    }
}

/*
===========================================================================
web.config <appSettings> entries required
===========================================================================

  <appSettings>

    <!-- SQL Server (metadata store) -->
    <add key="SqlConnectionString"
         value="Server=YOUR_SQL_SERVER;Database=YOUR_DB;User Id=YOUR_USER;Password=YOUR_PWD;" />

    <!-- Oracle (source of AECB XML) -->
    <!-- TNS-style Data Source for exaccnonprodcl04-scan.dibuat.ae -->
    <add key="OracleConnectionString"
         value="Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)
                (HOST=exaccnonprodcl04-scan.dibuat.ae)(PORT=1628)))
                (CONNECT_DATA=(SERVICE_NAME=SERUAT.dibuat.ae)));
                User Id=SOAAECB;Password=Ppa#987654;" />

  </appSettings>

===========================================================================
NuGet package required (install via Package Manager Console in VS 2012+):
===========================================================================

  Install-Package Oracle.ManagedDataAccess -Version 12.2.1100

===========================================================================
Calling example (Web Form / Service.asmx code-behind):
===========================================================================

  var provider = new AECBDataProvider();
  string xml = provider.GetXmlFromDatabase(emiratesId, passportNo);

===========================================================================
NOTE – Oracle table column assumption
===========================================================================
The Oracle query assumes SOAAECB.AECB_NAE_DATA contains columns:
  EMIRATESID  VARCHAR2
  PASSPORTNO  VARCHAR2
  AECBDATA    CLOB / XMLType / VARCHAR2
  REQUESTDATE DATE / TIMESTAMP

If the column names differ, update the WHERE clause in GetXmlFromOracle().
===========================================================================
*/
