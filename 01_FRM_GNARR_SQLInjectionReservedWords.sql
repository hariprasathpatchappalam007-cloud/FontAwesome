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
    (N'--', N'SQL single-line comment token', N'operator'),
    (N'/*', N'SQL block comment start token', N'operator'),
    (N'*/', N'SQL block comment end token', N'operator'),
    (N';', N'SQL statement separator', N'operator'),
    (N'@@', N'SQL Server global variable token', N'operator'),
    (N'@', N'SQL variable token', N'operator'),
    (N'char', N'SQL character conversion function', N'function'),
    (N'nchar', N'SQL unicode character conversion function', N'function'),
    (N'varchar', N'SQL varchar type keyword', N'keyword'),
    (N'nvarchar', N'SQL nvarchar type keyword', N'keyword'),
    (N'cast', N'SQL cast function', N'function'),
    (N'convert', N'SQL convert function', N'function'),
    (N'concat', N'SQL concat function', N'function'),
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
    (N'declare', N'SQL declare keyword', N'keyword'),
    (N'waitfor', N'SQL Server waitfor keyword', N'keyword'),
    (N'delay', N'SQL Server delay keyword', N'keyword'),
    (N'xp_', N'SQL Server extended stored procedure prefix', N'keyword'),
    (N'sp_', N'SQL Server stored procedure prefix', N'keyword'),
    (N'information_schema', N'SQL metadata schema', N'keyword'),
    (N'sysobjects', N'SQL Server metadata object table', N'keyword'),
    (N'syscolumns', N'SQL Server metadata column table', N'keyword');

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