/*
    Test cases for dbo.usp_ValidateArabicName.
    Run after ArabicNameValidation.sql and FRM_GNARR_Insert_ArabicRestrictedWords.sql.

    All Arabic literals must use N'...' so SQL Server passes Unicode text correctly.
*/

SET NOCOUNT ON;
GO

PRINT '01 - Individual Resident - valid first + last, full name derived internally';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'هاري',
    @LastName = N'براساث',
    @ResidencyType = N'Resident';
GO

PRINT '02 - Individual Non-Resident - valid first + middle + last, full name derived internally';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'محمد',
    @MiddleName = N'احمد',
    @LastName = N'علي',
    @ResidencyType = N'Non-Resident';
GO

PRINT '03 - Individual Resident - derived full name too short, should fail Full Name length';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'لجة',
    @LastName = N'ى',
    @ResidencyType = N'Resident';
GO

PRINT '04 - Individual Resident - first name missing, should fail First Name required';
EXEC dbo.usp_ValidateArabicName
    @FirstName = NULL,
    @LastName = N'براساث',
    @ResidencyType = N'Resident';
GO

PRINT '05 - Individual Resident - last name missing, should fail Last Name required';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'هاري',
    @LastName = NULL,
    @ResidencyType = N'Resident';
GO

PRINT '06 - Individual Resident - leading space, should fail spacing rule';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N' هاري',
    @LastName = N'براساث',
    @ResidencyType = N'Resident';
GO

PRINT '07 - Individual Resident - trailing space, should fail spacing rule';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'هاري ',
    @LastName = N'براساث',
    @ResidencyType = N'Resident';
GO

PRINT '08 - Individual Resident - consecutive spaces, should fail spacing rule';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'ها  ري',
    @LastName = N'براساث',
    @ResidencyType = N'Resident';
GO

PRINT '09 - Individual Resident - English characters, should fail Arabic-only rule';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'Hari',
    @LastName = N'براساث',
    @ResidencyType = N'Resident';
GO

PRINT '10 - Individual Resident - numbers, should fail Arabic-only rule';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'هاري1',
    @LastName = N'براساث',
    @ResidencyType = N'Resident';
GO

PRINT '11 - Individual Resident - restricted word exact match, should fail restricted word rule';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'محمد',
    @MiddleName = N'ابن',
    @LastName = N'علي',
    @ResidencyType = N'Resident';
GO

PRINT '12 - Individual Resident - restricted word partial match, should not fail restricted word rule';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'محمد',
    @MiddleName = N'ابناء',
    @LastName = N'علي',
    @ResidencyType = N'Resident';
GO

PRINT '13 - Individual Resident - allowed Arabic only, no special characters';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'عبدالله',
    @MiddleName = N'محمد',
    @LastName = N'سالم',
    @ResidencyType = N'Resident';
GO

PRINT '14 - Resident-Company - valid Arabic, numbers, spaces, and allowed special characters';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'شركة السلام 123 / \ - _ & . ( ) , @ '' < >',
    @LastName = N'فرع دبي 45',
    @ResidencyType = N'Resident-Company';
GO

PRINT '15 - Non-Resident-Company - valid Arabic and numbers only';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'شركة النور 2026',
    @LastName = N'فرع 12',
    @ResidencyType = N'Non-Resident-Company';
GO

PRINT '16 - Resident-Company - invalid pipe character, should return IsValid = 0';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'شركة السلام | دبي',
    @LastName = N'فرع دبي',
    @ResidencyType = N'Resident-Company';
GO

PRINT '17 - Resident-Company - invalid English letters, should return IsValid = 0';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'ABC شركة',
    @LastName = N'فرع دبي',
    @ResidencyType = N'Resident-Company';
GO

PRINT '18 - Non-Resident-Company - invalid exclamation mark, should return IsValid = 0';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'شركة السلام!',
    @LastName = N'فرع دبي',
    @ResidencyType = N'Non-Resident-Company';
GO

PRINT '19 - Resident-Company - leading space, should fail spacing rule as error';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N' شركة السلام',
    @LastName = N'فرع دبي',
    @ResidencyType = N'Resident-Company';
GO

PRINT '20 - Resident-Company - trailing space, should fail spacing rule as error';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'شركة السلام ',
    @LastName = N'فرع دبي',
    @ResidencyType = N'Resident-Company';
GO

PRINT '21 - Resident-Company - consecutive spaces, should fail spacing rule as error';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'شركة  السلام',
    @LastName = N'فرع دبي',
    @ResidencyType = N'Resident-Company';
GO

PRINT '22 - Resident-Company - missing first name, should fail First Name required';
EXEC dbo.usp_ValidateArabicName
    @FirstName = NULL,
    @LastName = N'فرع دبي',
    @ResidencyType = N'Resident-Company';
GO

PRINT '23 - Resident-Company - missing last name, should fail Last Name required';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'شركة السلام',
    @LastName = NULL,
    @ResidencyType = N'Resident-Company';
GO

PRINT '24 - Resident-Company - invalid characters in first name only; no separate Full Name invalid-character message expected';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'شركة السلام!',
    @MiddleName = N'الشرق',
    @LastName = N'فرع دبي',
    @ResidencyType = N'Resident-Company';
GO

PRINT '25 - No residency type - first name less than 4, should apply normal field minimum length';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'ها',
    @LastName = N'براساث';
GO

PRINT '26 - No residency type - middle name less than 4 when provided, should apply normal field minimum length';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'هاري',
    @MiddleName = N'ها',
    @LastName = N'براساث';
GO

PRINT '27 - Unicode literal reminder - correct Arabic call uses N prefix';
EXEC dbo.usp_ValidateArabicName
    @FirstName = N'هاري',
    @LastName = N'براساث',
    @ResidencyType = N'Resident';
GO

/*
    Do not use this format for Arabic text:

    EXEC dbo.usp_ValidateArabicName
        @FirstName = 'هاري',
        @LastName = 'براساث',
        @ResidencyType = N'Resident';

    Without N, SQL Server may convert Arabic text to question marks before procedure validation.
*/