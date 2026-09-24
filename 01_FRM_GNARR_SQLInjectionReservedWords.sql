/*
    Purpose:
    Seed SQL-injection restricted words/tokens into FRM_GNARR and expose only
    active rows through a small procedure for the application layer.

    Important:
    This is a defensive input validation layer. It must not replace parameterized
    queries / SqlParameter usage in C# and stored procedures.
*/

SET NOCOUNT ON;

DECLARE @Code nvarchar(200) = N'SQL_INJECTION_RESERVED_TOKEN';
DECLARE @CreatedBy nvarchar(100) = N'system';

DECLARE @ReservedTokens table
(
    Value nvarchar(200) NOT NULL,
    DisplayValue nvarchar(1000) NULL,
    PassValue nvarchar(100) NULL
);

INSERT INTO @ReservedTokens (Value, DisplayValue, PassValue)
VALUES
    (N'''', N'SQL string literal quote token', N'operator'),
    (N'"', N'Double quote token', N'operator'),
    (N'--', N'SQL single-line comment token', N'operator'),
    (N'/*', N'SQL block comment start token', N'operator'),
    (N'*/', N'SQL block comment end token', N'operator'),
    (N';', N'SQL statement separator', N'operator'),
    (N'@@', N'SQL Server global variable token', N'operator'),
    (N'@', N'SQL variable token', N'operator'),
    (N'[', N'SQL delimited identifier start token', N'operator'),
    (N']', N'SQL delimited identifier end token', N'operator'),
    (N'(', N'SQL expression start token', N'operator'),
    (N')', N'SQL expression end token', N'operator'),
    (N'=', N'SQL equality operator', N'operator'),
    (N'<>', N'SQL not equal operator', N'operator'),
    (N'!=', N'SQL not equal operator', N'operator'),
    (N'<', N'SQL less-than operator', N'operator'),
    (N'>', N'SQL greater-than operator', N'operator'),
    (N'%', N'SQL wildcard or modulo token', N'operator'),
    (N'+', N'SQL addition or concatenation operator', N'operator'),
    (N'|', N'SQL bitwise OR operator', N'operator'),
    (N'&', N'SQL bitwise AND operator', N'operator'),
    (N'0x', N'SQL hexadecimal literal prefix', N'operator'),
    (N'char', N'SQL character conversion function', N'function'),
    (N'nchar', N'SQL unicode character conversion function', N'function'),
    (N'varchar', N'SQL varchar type keyword', N'keyword'),
    (N'nvarchar', N'SQL nvarchar type keyword', N'keyword'),
    (N'ascii', N'SQL ascii function', N'function'),
    (N'unicode', N'SQL unicode function', N'function'),
    (N'substring', N'SQL substring function', N'function'),
    (N'patindex', N'SQL pattern index function', N'function'),
    (N'quotename', N'SQL quote name function', N'function'),
    (N'cast', N'SQL cast function', N'function'),
    (N'convert', N'SQL convert function', N'function'),
    (N'concat', N'SQL concat function', N'function'),
    (N'coalesce', N'SQL coalesce function', N'function'),
    (N'isnull', N'SQL isnull function', N'function'),
    (N'select', N'SQL select keyword', N'keyword'),
    (N'insert', N'SQL insert keyword', N'keyword'),
    (N'update', N'SQL update keyword', N'keyword'),
    (N'delete', N'SQL delete keyword', N'keyword'),
    (N'drop', N'SQL drop keyword', N'keyword'),
    (N'alter', N'SQL alter keyword', N'keyword'),
    (N'create', N'SQL create keyword', N'keyword'),
    (N'truncate', N'SQL truncate keyword', N'keyword'),
    (N'exec', N'SQL exec keyword', N'keyword'),
    (N'execute', N'SQL execute keyword', N'keyword'),
    (N'union', N'SQL union keyword', N'keyword'),
    (N'intersect', N'SQL intersect keyword', N'keyword'),
    (N'except', N'SQL except keyword', N'keyword'),
    (N'declare', N'SQL declare keyword', N'keyword'),
    (N'cursor', N'SQL cursor keyword', N'keyword'),
    (N'fetch', N'SQL fetch keyword', N'keyword'),
    (N'open', N'SQL open keyword', N'keyword'),
    (N'close', N'SQL close keyword', N'keyword'),
    (N'deallocate', N'SQL deallocate keyword', N'keyword'),
    (N'begin', N'SQL begin keyword', N'keyword'),
    (N'end', N'SQL end keyword', N'keyword'),
    (N'if', N'SQL if keyword', N'keyword'),
    (N'else', N'SQL else keyword', N'keyword'),
    (N'while', N'SQL while keyword', N'keyword'),
    (N'try', N'SQL try keyword', N'keyword'),
    (N'catch', N'SQL catch keyword', N'keyword'),
    (N'print', N'SQL print keyword', N'keyword'),
    (N'raiserror', N'SQL raiserror keyword', N'keyword'),
    (N'throw', N'SQL throw keyword', N'keyword'),
    (N'grant', N'SQL grant keyword', N'keyword'),
    (N'revoke', N'SQL revoke keyword', N'keyword'),
    (N'deny', N'SQL deny keyword', N'keyword'),
    (N'backup', N'SQL backup keyword', N'keyword'),
    (N'restore', N'SQL restore keyword', N'keyword'),
    (N'kill', N'SQL kill keyword', N'keyword'),
    (N'use', N'SQL use database keyword', N'keyword'),
    (N'where', N'SQL where keyword', N'keyword'),
    (N'having', N'SQL having keyword', N'keyword'),
    (N'group', N'SQL group keyword', N'keyword'),
    (N'order', N'SQL order keyword', N'keyword'),
    (N'by', N'SQL by keyword', N'keyword'),
    (N'or', N'SQL OR operator keyword', N'keyword'),
    (N'and', N'SQL AND operator keyword', N'keyword'),
    (N'not', N'SQL NOT operator keyword', N'keyword'),
    (N'like', N'SQL like keyword', N'keyword'),
    (N'in', N'SQL in keyword', N'keyword'),
    (N'exists', N'SQL exists keyword', N'keyword'),
    (N'waitfor', N'SQL Server waitfor keyword', N'keyword'),
    (N'delay', N'SQL Server delay keyword', N'keyword'),
    (N'set', N'SQL set keyword', N'keyword'),
    (N'arithabort', N'SQL Server SET ARITHABORT option', N'keyword'),
    (N'ansi_nulls', N'SQL Server SET ANSI_NULLS option', N'keyword'),
    (N'ansi_warnings', N'SQL Server SET ANSI_WARNINGS option', N'keyword'),
    (N'quoted_identifier', N'SQL Server SET QUOTED_IDENTIFIER option', N'keyword'),
    (N'concat_null_yields_null', N'SQL Server SET CONCAT_NULL_YIELDS_NULL option', N'keyword'),
    (N'numeric_roundabort', N'SQL Server SET NUMERIC_ROUNDABORT option', N'keyword'),
    (N'xact_abort', N'SQL Server SET XACT_ABORT option', N'keyword'),
    (N'noexec', N'SQL Server SET NOEXEC option', N'keyword'),
    (N'parseonly', N'SQL Server SET PARSEONLY option', N'keyword'),
    (N'showplan_all', N'SQL Server SET SHOWPLAN_ALL option', N'keyword'),
    (N'showplan_text', N'SQL Server SET SHOWPLAN_TEXT option', N'keyword'),
    (N'showplan_xml', N'SQL Server SET SHOWPLAN_XML option', N'keyword'),
    (N'xp_', N'SQL Server extended stored procedure prefix', N'keyword'),
    (N'sp_', N'SQL Server stored procedure prefix', N'keyword'),
    (N'information_schema', N'SQL metadata schema', N'keyword'),
    (N'sysobjects', N'SQL Server metadata object table', N'keyword'),
    (N'syscolumns', N'SQL Server metadata column table', N'keyword'),
    (N'sysdatabases', N'SQL Server metadata database table', N'keyword'),
    (N'sysusers', N'SQL Server metadata users table', N'keyword'),
    (N'syslogins', N'SQL Server metadata logins table', N'keyword'),
    (N'sysprocesses', N'SQL Server metadata processes table', N'keyword'),
    (N'syscomments', N'SQL Server metadata comments table', N'keyword'),
    (N'sys.tables', N'SQL Server catalog tables view', N'keyword'),
    (N'sys.columns', N'SQL Server catalog columns view', N'keyword'),
    (N'sys.objects', N'SQL Server catalog objects view', N'keyword'),
    (N'sys.schemas', N'SQL Server catalog schemas view', N'keyword'),
    (N'sys.sql_modules', N'SQL Server catalog module definitions view', N'keyword'),
    (N'bigint', N'SQL Server bigint data type', N'datatype'),
    (N'int', N'SQL Server int data type', N'datatype'),
    (N'smallint', N'SQL Server smallint data type', N'datatype'),
    (N'tinyint', N'SQL Server tinyint data type', N'datatype'),
    (N'bit', N'SQL Server bit data type', N'datatype'),
    (N'decimal', N'SQL Server decimal data type', N'datatype'),
    (N'numeric', N'SQL Server numeric data type', N'datatype'),
    (N'money', N'SQL Server money data type', N'datatype'),
    (N'smallmoney', N'SQL Server smallmoney data type', N'datatype'),
    (N'float', N'SQL Server float data type', N'datatype'),
    (N'real', N'SQL Server real data type', N'datatype'),
    (N'date', N'SQL Server date data type', N'datatype'),
    (N'time', N'SQL Server time data type', N'datatype'),
    (N'datetime', N'SQL Server datetime data type', N'datatype'),
    (N'datetime2', N'SQL Server datetime2 data type', N'datatype'),
    (N'smalldatetime', N'SQL Server smalldatetime data type', N'datatype'),
    (N'datetimeoffset', N'SQL Server datetimeoffset data type', N'datatype'),
    (N'text', N'SQL Server text data type', N'datatype'),
    (N'ntext', N'SQL Server ntext data type', N'datatype'),
    (N'binary', N'SQL Server binary data type', N'datatype'),
    (N'varbinary', N'SQL Server varbinary data type', N'datatype'),
    (N'image', N'SQL Server image data type', N'datatype'),
    (N'rowversion', N'SQL Server rowversion data type', N'datatype'),
    (N'timestamp', N'SQL Server timestamp data type', N'datatype'),
    (N'uniqueidentifier', N'SQL Server uniqueidentifier data type', N'datatype'),
    (N'sql_variant', N'SQL Server sql_variant data type', N'datatype'),
    (N'xml', N'SQL Server xml data type', N'datatype'),
    (N'table', N'SQL Server table data type', N'datatype'),
    (N'hierarchyid', N'SQL Server hierarchyid data type', N'datatype'),
    (N'geometry', N'SQL Server geometry data type', N'datatype'),
    (N'geography', N'SQL Server geography data type', N'datatype'),
    (N'sysname', N'SQL Server sysname data type alias', N'datatype');

MERGE dbo.FRM_GNARR AS target
USING @ReservedTokens AS source
    ON target.Code = @Code
    AND target.Value = source.Value
WHEN MATCHED THEN
    UPDATE SET
        target.DisplayValue = source.DisplayValue,
        target.Status = N'a',
        target.PassValue = source.PassValue
WHEN NOT MATCHED THEN
    INSERT
    (
        Code,
        Value,
        DisplayValue,
        Status,
        createdby_User,
        createdon_date,
        PassValue
    )
    VALUES
    (
        @Code,
        source.Value,
        source.DisplayValue,
        N'a',
        @CreatedBy,
        GETDATE(),
        source.PassValue
    );
GO

CREATE OR ALTER PROCEDURE dbo.usp_FRM_GNARR_GetSqlInjectionReservedTokens
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Value,
        DisplayValue,
        PassValue
    FROM dbo.FRM_GNARR
    WHERE Code = N'SQL_INJECTION_RESERVED_TOKEN'
      AND Status = N'a'
    ORDER BY Value;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FRM_GNARR_ValidateSqlInjectionText
    @InputValue nvarchar(max),
    @IsValid bit OUTPUT,
    @MatchedToken nvarchar(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @IsValid = 1;
    SET @MatchedToken = NULL;

    IF @InputValue IS NULL OR LTRIM(RTRIM(@InputValue)) = N''
    BEGIN
        RETURN;
    END;

    SELECT TOP (1)
        @MatchedToken = Value
    FROM dbo.FRM_GNARR
    WHERE Code = N'SQL_INJECTION_RESERVED_TOKEN'
      AND Status = N'a'
      AND CHARINDEX(Value, @InputValue) > 0
    ORDER BY LEN(Value) DESC;

    IF @MatchedToken IS NOT NULL
    BEGIN
        SET @IsValid = 0;
    END;
END;
GO