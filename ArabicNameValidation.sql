/*
    Arabic name validation setup for SQL Server.

    This script stores Arabic restricted text in the existing FRM_GNARR table and exposes
    dbo.usp_ValidateArabicName to validate first, middle, last, and backend full name values.
*/

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.FRM_GNARR', N'U') IS NULL
BEGIN
    RAISERROR('Required table dbo.FRM_GNARR does not exist.', 16, 1);
    RETURN;
END;
GO

INSERT INTO dbo.FRM_GNARR
(
    Code,
    Value,
    DisplayValue,
    Status,
    createdby_User,
    createdon_date,
    PassValue,
    DISPLAYVALUE_AR
)
SELECT
    Source.Code,
    Source.ArabicWord,
    Source.EnglishMeaning,
    N'A',
    N'SYSTEM',
    GETDATE(),
    N'ArabicNameRestrictedWord',
    Source.ArabicWord
FROM
(
    VALUES
        (N'ARABIC_NAME_RESTRICTED_WORD', N'زوجة', N'Wife / Spouse (female)'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'و', N'And'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'مزرعة', N'Farm'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'مزرعه', N'Farm'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'ام', N'Mother / Umm'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'أم', N'Mother / Umm'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'والدة', N'Mother (formal)'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'الوصي', N'Guardian / Legal Custodian'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'وصي', N'Guardian / Legal Custodian'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'إبن', N'Son of'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'ابن', N'Son of'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'ابنه', N'Daughter of'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'إبنه', N'Daughter of'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'ابنة', N'Daughter of'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'إبنة', N'Daughter of'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'مبنى', N'Building'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'بناء', N'Construction / Building'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'ارملة', N'Widow'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'أرملة', N'Widow'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'ارمله', N'Widow'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'أرمله', N'Widow'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'قاصر', N'Minor (under legal age)'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'مشروع', N'Project'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'تنازل', N'Waiver / Assignment / Transfer of Rights'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'بالتنازل', N'Waiver / Assignment / Transfer of Rights'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'التنازل', N'Waiver / Assignment / Transfer of Rights'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'محكمة', N'Court'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'محكمه', N'Court'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'وريث', N'Heir'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'ورثة', N'Heirs / Beneficiaries'),
        (N'ARABIC_NAME_RESTRICTED_WORD', N'ورثه', N'Heirs / Beneficiaries')
) AS Source (Code, ArabicWord, EnglishMeaning)
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.FRM_GNARR AS ExistingConfig
    WHERE ExistingConfig.Code = Source.Code
      AND ExistingConfig.Value = Source.ArabicWord
);
GO

CREATE OR ALTER PROCEDURE dbo.usp_ValidateArabicName
    @FirstName NVARCHAR(200) = NULL,
    @MiddleName NVARCHAR(200) = NULL,
    @LastName NVARCHAR(200) = NULL,
    @FullName NVARCHAR(600) = NULL,
    @ResidencyType NVARCHAR(50) = NULL,
    @ApplicantType NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Errors TABLE
    (
        ErrorId INT IDENTITY(1,1) NOT NULL,
        FieldName NVARCHAR(50) NOT NULL,
        ErrorMessage NVARCHAR(300) NOT NULL
    );

    DECLARE @NormalizedResidencyType NVARCHAR(50) = LTRIM(RTRIM(ISNULL(@ResidencyType, N'')));
    DECLARE @IsIndividual BIT = CASE WHEN @NormalizedResidencyType IN (N'Resident', N'Non-Resident') THEN 1 ELSE 0 END;

    DECLARE @Fields TABLE
    (
        FieldOrder INT NOT NULL,
        FieldName NVARCHAR(50) NOT NULL,
        FieldValue NVARCHAR(600) NULL,
        IsRequired BIT NOT NULL,
        ApplyMinimumLength BIT NOT NULL
    );

    INSERT INTO @Fields (FieldOrder, FieldName, FieldValue, IsRequired, ApplyMinimumLength)
    VALUES
        (1, N'First Name', @FirstName, 1, CASE WHEN @IsIndividual = 1 THEN 0 ELSE 1 END),
        (2, N'Middle Name', @MiddleName, 0, CASE WHEN @IsIndividual = 1 THEN 0 ELSE 1 END),
        (3, N'Last Name', @LastName, 1, CASE WHEN @IsIndividual = 1 THEN 0 ELSE 1 END),
        (4, N'Full Name', @FullName, 0, 1);

    DECLARE
        @FieldOrder INT,
        @FieldName NVARCHAR(50),
        @OriginalValue NVARCHAR(600),
        @NormalizedValue NVARCHAR(600),
        @IsRequired BIT,
        @ApplyMinimumLength BIT,
        @CharacterIndex INT,
        @CurrentCode INT,
        @HasArabic BIT,
        @HasInvalidIndividualCharacter BIT,
        @HasInvalidNonIndividualCharacter BIT;

    DECLARE FieldCursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT FieldOrder, FieldName, FieldValue, IsRequired, ApplyMinimumLength
        FROM @Fields
        ORDER BY FieldOrder;

    OPEN FieldCursor;
    FETCH NEXT FROM FieldCursor INTO @FieldOrder, @FieldName, @OriginalValue, @IsRequired, @ApplyMinimumLength;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @NormalizedValue = ISNULL(@OriginalValue, N'');
        SET @NormalizedValue = REPLACE(@NormalizedValue, NCHAR(0x00A0), N' ');
        SET @NormalizedValue = REPLACE(@NormalizedValue, NCHAR(0x200F), N'');
        SET @NormalizedValue = REPLACE(@NormalizedValue, NCHAR(0x200E), N'');
        SET @NormalizedValue = REPLACE(@NormalizedValue, NCHAR(0x200B), N'');
        SET @NormalizedValue = LTRIM(RTRIM(@NormalizedValue));

        WHILE CHARINDEX(N'  ', @NormalizedValue) > 0
        BEGIN
            SET @NormalizedValue = REPLACE(@NormalizedValue, N'  ', N' ');
        END;

        SET @CharacterIndex = 1;
        SET @HasArabic = 0;
        SET @HasInvalidIndividualCharacter = 0;
        SET @HasInvalidNonIndividualCharacter = 0;

        WHILE @CharacterIndex <= LEN(@NormalizedValue)
        BEGIN
            SET @CurrentCode = UNICODE(SUBSTRING(@NormalizedValue, @CharacterIndex, 1));

            IF @CurrentCode BETWEEN 1536 AND 1791
            BEGIN
                SET @HasArabic = 1;
            END
            ELSE IF @CurrentCode <> 32
            BEGIN
                SET @HasInvalidIndividualCharacter = 1;
            END;

            IF NOT
            (
                @CurrentCode BETWEEN 1536 AND 1791
                OR @CurrentCode BETWEEN 48 AND 57
                OR @CurrentCode IN (32, 38, 39, 40, 41, 44, 45, 46, 47, 60, 62, 64, 92, 95, 124)
            )
            BEGIN
                SET @HasInvalidNonIndividualCharacter = 1;
            END;

            SET @CharacterIndex += 1;
        END;

        IF ISNULL(@OriginalValue, N'') <> @NormalizedValue
        BEGIN
            INSERT INTO @Errors (FieldName, ErrorMessage)
            VALUES (@FieldName, @FieldName + N' should not have leading or trailing spaces or consecutive spaces');
        END
        ELSE IF @IsRequired = 1 AND LEN(@NormalizedValue) = 0
        BEGIN
            INSERT INTO @Errors (FieldName, ErrorMessage)
            VALUES (@FieldName, @FieldName + N' is required');
        END
        ELSE IF LEN(@NormalizedValue) > 0 AND @HasArabic = 0
        BEGIN
            INSERT INTO @Errors (FieldName, ErrorMessage)
            VALUES (@FieldName, @FieldName + N' is invalid');
        END
        ELSE IF @ApplyMinimumLength = 1 AND LEN(@NormalizedValue) > 0 AND LEN(@NormalizedValue) < 4
        BEGIN
            INSERT INTO @Errors (FieldName, ErrorMessage)
            VALUES (@FieldName, @FieldName + N' must be at least 4 characters');
        END
        ELSE IF @IsIndividual = 1 AND LEN(@NormalizedValue) > 0 AND @HasInvalidIndividualCharacter = 1
        BEGIN
            INSERT INTO @Errors (FieldName, ErrorMessage)
            VALUES (@FieldName, @FieldName + N' must contain only Arabic letters');
        END
        ELSE IF @IsIndividual = 0 AND LEN(@NormalizedValue) > 0 AND @HasInvalidNonIndividualCharacter = 1
        BEGIN
            INSERT INTO @Errors (FieldName, ErrorMessage)
            VALUES (@FieldName, @FieldName + N' contains invalid characters');
        END
        ELSE IF @IsIndividual = 1 AND LEN(@NormalizedValue) > 0 AND EXISTS
        (
            SELECT 1
            FROM dbo.FRM_GNARR AS RestrictedWords
            WHERE RestrictedWords.Code = N'ARABIC_NAME_RESTRICTED_WORD'
              AND ISNULL(RestrictedWords.Status, N'A') = N'A'
              AND CHARINDEX(N' ' + RestrictedWords.Value + N' ', N' ' + @NormalizedValue + N' ') > 0
        )
        BEGIN
            INSERT INTO @Errors (FieldName, ErrorMessage)
            VALUES (@FieldName, @FieldName + N' contains restricted Arabic words');
        END;

        FETCH NEXT FROM FieldCursor INTO @FieldOrder, @FieldName, @OriginalValue, @IsRequired, @ApplyMinimumLength;
    END;

    CLOSE FieldCursor;
    DEALLOCATE FieldCursor;

    SELECT
        CAST(CASE WHEN EXISTS (SELECT 1 FROM @Errors) THEN 0 ELSE 1 END AS BIT) AS IsValid;

    SELECT
        ErrorId,
        FieldName,
        ErrorMessage
    FROM @Errors
    ORDER BY ErrorId;
END;
GO

/*
    Sample executions.
    Arabic string literals must use the N prefix, otherwise SQL Server can convert
    the text through a non-Unicode code page before it reaches the NVARCHAR parameters.

    Valid non-individual example:
    EXEC dbo.usp_ValidateArabicName
        @FirstName = N'هاري',
        @LastName = N'براساث';

    Valid individual example:
    EXEC dbo.usp_ValidateArabicName
        @FirstName = N'هاري',
        @LastName = N'براساث',
        @FullName = N'هاري براساث',
        @ResidencyType = N'Resident';
*/