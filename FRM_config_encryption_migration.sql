/*
    FRM_config password migration from encoded text to strong SQL Server encryption.

    Assumptions:
    - Database engine is Microsoft SQL Server.
    - FRM_config.Key_Value currently stores Base64-encoded values when IsPassword = 1.
    - Password values will be migrated in-place in Key_Value as ENC:<base64 encrypted bytes>.
    - Non-password values must remain plain text in Key_Value.
    - Password values should be returned decrypted only through dbo.usp_FRM_config_Get.
    - Password values should be saved encrypted only through dbo.usp_FRM_config_Save.

    Run in the target application database as a deployment user with permissions to
    create master key, certificate, symmetric key, columns, functions, and procedures.
*/

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
SET NOCOUNT ON;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.FRM_config')
      AND c.name = N'Key_Value'
      AND (t.name <> N'nvarchar' OR c.max_length <> -1)
)
BEGIN
    ALTER TABLE dbo.FRM_config ALTER COLUMN Key_Value nvarchar(max) NULL;
END;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##')
    BEGIN
        /* Change this password before running in production and store it in the bank-approved secret vault. */
        CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'dib@hfserviceportal-uat@lms*13579!';
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.certificates WHERE name = 'FRM_Config_DataProtection_Cert')
    BEGIN
        CREATE CERTIFICATE FRM_Config_DataProtection_Cert
            WITH SUBJECT = 'FRM_config password encryption certificate',
                 EXPIRY_DATE = '2099-12-31';
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.symmetric_keys WHERE name = 'FRM_Config_AES256_Key')
    BEGIN
        CREATE SYMMETRIC KEY FRM_Config_AES256_Key
            WITH ALGORITHM = AES_256
            ENCRYPTION BY CERTIFICATE FRM_Config_DataProtection_Cert;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

CREATE OR ALTER FUNCTION dbo.ufn_FRM_Base64DecodeToNvarchar
(
    @EncodedValue nvarchar(max)
)
RETURNS nvarchar(max)
AS
BEGIN
    DECLARE @DecodedBytes varbinary(max);

    IF @EncodedValue IS NULL OR LTRIM(RTRIM(@EncodedValue)) = N''
        RETURN @EncodedValue;

    SET @DecodedBytes = CAST(N'' AS xml).value(
        'xs:base64Binary(sql:variable("@EncodedValue"))',
        'varbinary(max)'
    );

    RETURN CONVERT(nvarchar(max), CONVERT(varchar(max), @DecodedBytes));
END;
GO

CREATE OR ALTER FUNCTION dbo.ufn_FRM_Base64EncodeFromVarbinary
(
    @Bytes varbinary(max)
)
RETURNS nvarchar(max)
AS
BEGIN
    IF @Bytes IS NULL
        RETURN NULL;

    RETURN CAST(N'' AS xml).value(
        'xs:base64Binary(sql:variable("@Bytes"))',
        'nvarchar(max)'
    );
END;
GO

CREATE OR ALTER FUNCTION dbo.ufn_FRM_Base64DecodeToVarbinary
(
    @EncodedValue nvarchar(max)
)
RETURNS varbinary(max)
AS
BEGIN
    IF @EncodedValue IS NULL OR LTRIM(RTRIM(@EncodedValue)) = N''
        RETURN NULL;

    RETURN CAST(N'' AS xml).value(
        'xs:base64Binary(sql:variable("@EncodedValue"))',
        'varbinary(max)'
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FRM_config_MigrateEncodedPasswords
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    OPEN SYMMETRIC KEY FRM_Config_AES256_Key
        DECRYPTION BY CERTIFICATE FRM_Config_DataProtection_Cert;

    UPDATE dbo.FRM_config
       SET Key_Value = N'ENC:' + dbo.ufn_FRM_Base64EncodeFromVarbinary(
               EncryptByKey(
                   Key_GUID(N'FRM_Config_AES256_Key'),
                   CONVERT(varbinary(max), dbo.ufn_FRM_Base64DecodeToNvarchar(Key_Value)),
                   1,
                   Key_Name
               )
           ),
                     Updatedon_Date = COALESCE(Updatedon_Date, GETDATE())
     WHERE ISNULL(IsPassword, 0) = 1
       AND Key_Value IS NOT NULL
       AND Key_Value NOT LIKE N'ENC:%';

    CLOSE SYMMETRIC KEY FRM_Config_AES256_Key;

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FRM_config_Get
(
    @Key_Name nvarchar(255) = NULL,
    @Status nvarchar(10) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    OPEN SYMMETRIC KEY FRM_Config_AES256_Key
        DECRYPTION BY CERTIFICATE FRM_Config_DataProtection_Cert;

    SELECT
        Key_Name,
        CASE
            WHEN ISNULL(IsPassword, 0) = 1 AND Key_Value LIKE N'ENC:%'
                THEN CONVERT(nvarchar(max), DecryptByKey(dbo.ufn_FRM_Base64DecodeToVarbinary(SUBSTRING(Key_Value, 5, LEN(Key_Value))), 1, Key_Name))
            WHEN ISNULL(IsPassword, 0) = 1 AND Key_Value IS NOT NULL
                THEN dbo.ufn_FRM_Base64DecodeToNvarchar(Key_Value)
            ELSE Key_Value
        END AS Key_Value,
        Status,
        Createdby_user,
        Createdon_date,
        Remarks,
        Updatedby_user,
        Updatedon_Date,
        IsPassword
    FROM dbo.FRM_config
    WHERE (@Key_Name IS NULL OR Key_Name = @Key_Name)
      AND (@Status IS NULL OR Status = @Status);

    CLOSE SYMMETRIC KEY FRM_Config_AES256_Key;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FRM_config_Save
(
    @Key_Name nvarchar(255),
    @Key_Value nvarchar(max),
    @Status nvarchar(10),
    @Remarks nvarchar(max) = NULL,
    @IsPassword bit = 0,
    @User_Name nvarchar(255) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @EffectiveUser nvarchar(255) = COALESCE(@User_Name, SUSER_SNAME());
    DECLARE @EncryptedBytes varbinary(max) = NULL;
    DECLARE @EncryptedValue nvarchar(max) = NULL;

    IF @IsPassword = 1 AND @Key_Value IS NOT NULL
    BEGIN
        OPEN SYMMETRIC KEY FRM_Config_AES256_Key
            DECRYPTION BY CERTIFICATE FRM_Config_DataProtection_Cert;

        SET @EncryptedBytes = EncryptByKey(
            Key_GUID(N'FRM_Config_AES256_Key'),
            CONVERT(varbinary(max), @Key_Value),
            1,
            @Key_Name
        );

        SET @EncryptedValue = N'ENC:' + dbo.ufn_FRM_Base64EncodeFromVarbinary(@EncryptedBytes);

        CLOSE SYMMETRIC KEY FRM_Config_AES256_Key;
    END;

    IF EXISTS (SELECT 1 FROM dbo.FRM_config WHERE Key_Name = @Key_Name)
    BEGIN
        UPDATE dbo.FRM_config
           SET Key_Value = CASE WHEN @IsPassword = 1 THEN @EncryptedValue ELSE @Key_Value END,
               Status = @Status,
               Remarks = @Remarks,
               IsPassword = @IsPassword,
               Updatedby_user = @EffectiveUser,
               Updatedon_Date = GETDATE()
         WHERE Key_Name = @Key_Name;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.FRM_config
        (
            Key_Name,
            Key_Value,
            Status,
            Createdby_user,
            Createdon_date,
            Remarks,
            Updatedby_user,
            Updatedon_Date,
            IsPassword
        )
        VALUES
        (
            @Key_Name,
            CASE WHEN @IsPassword = 1 THEN @EncryptedValue ELSE @Key_Value END,
            @Status,
            @EffectiveUser,
            GETDATE(),
            @Remarks,
            @EffectiveUser,
            GETDATE(),
            @IsPassword
        );
    END;
END;
GO

GRANT EXECUTE ON SCHEMA::dbo TO public;
GO

EXEC dbo.usp_FRM_config_MigrateEncodedPasswords;
GO

SELECT Key_Name, IsPassword,
    CASE WHEN IsPassword = 1 AND Key_Value LIKE N'ENC:%' THEN 'EncryptedInKeyValue' ELSE 'PlainTextOrLegacyEncoded' END AS StorageState
FROM dbo.FRM_config
WHERE ISNULL(IsPassword, 0) = 1;
GO

/*
        Recommended after application code has been changed to use dbo.usp_FRM_config_Get
        and dbo.usp_FRM_config_Save, and after backup validation.

    BACKUP CERTIFICATE FRM_Config_DataProtection_Cert
        TO FILE = 'D:\SQLBackups\FRM_Config_DataProtection_Cert.cer'
        WITH PRIVATE KEY
        (
            FILE = 'D:\SQLBackups\FRM_Config_DataProtection_Cert_PrivateKey.pvk',
            ENCRYPTION BY PASSWORD = 'CHANGE_ME_TO_A_DIFFERENT_LONG_RANDOM_BACKUP_SECRET_2026!'
        );
*/