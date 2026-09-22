/*
    Insert Arabic name restricted words into existing dbo.FRM_GNARR table.
    Run this script before dbo.usp_ValidateArabicName is used.
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
    N'Active',
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