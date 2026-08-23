IF OBJECT_ID('dbo.usp_IsUserAuthorizedForPage', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.usp_IsUserAuthorizedForPage;
END
GO

CREATE PROCEDURE dbo.usp_IsUserAuthorizedForPage
    @LoginId NVARCHAR(100),
    @RequestPath NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @NormalizedPath NVARCHAR(500);
    DECLARE @QuestionIndex INT;

    SET @NormalizedPath = UPPER(LTRIM(RTRIM(ISNULL(@RequestPath, ''))));

    SET @QuestionIndex = CHARINDEX('?', @NormalizedPath);
    IF @QuestionIndex > 0
        SET @NormalizedPath = LEFT(@NormalizedPath, @QuestionIndex - 1);

    WHILE LEN(@NormalizedPath) > 1 AND RIGHT(@NormalizedPath, 1) = '/'
        SET @NormalizedPath = LEFT(@NormalizedPath, LEN(@NormalizedPath) - 1);

    ;WITH MenuData AS
    (
        SELECT
            UPPER(LTRIM(RTRIM(MNU.Menu_filename))) AS MenuFileName,
            CASE
                WHEN CHARINDEX('?', UPPER(LTRIM(RTRIM(MNU.Menu_filename)))) > 0
                    THEN LEFT(UPPER(LTRIM(RTRIM(MNU.Menu_filename))), CHARINDEX('?', UPPER(LTRIM(RTRIM(MNU.Menu_filename)))) - 1)
                ELSE UPPER(LTRIM(RTRIM(MNU.Menu_filename)))
            END AS MenuPathOnly,
            UPPER(LTRIM(RTRIM(MNU.Menu_Role))) AS MenuRole,
            MNU.Status AS MenuStatus
        FROM FRM_MENU MNU
    ),
    UserData AS
    (
        SELECT
            UPPER(LTRIM(RTRIM(USR.LoginID))) AS LoginId,
            UPPER(LTRIM(RTRIM(RLE.USER_ROLE))) AS UserRole,
            USR.Status AS UserStatus,
            RLE.Status AS UserRoleStatus
        FROM FRM_USER USR
        INNER JOIN FRM_USERROLE RLE
            ON RLE.USER_USERID = USR.LoginID
    )
    SELECT
        M.MenuFileName,
        M.MenuRole,
        U.LoginId
    FROM MenuData M
    INNER JOIN UserData U
        ON U.UserRole = M.MenuRole
    WHERE U.LoginId = UPPER(LTRIM(RTRIM(@LoginId)))
      AND U.UserStatus = 'A'
      AND U.UserRoleStatus = 'A'
      AND M.MenuStatus = 'A'
      AND CASE
              WHEN LEN(M.MenuPathOnly) > 1 AND RIGHT(M.MenuPathOnly, 1) = '/'
                  THEN LEFT(M.MenuPathOnly, LEN(M.MenuPathOnly) - 1)
              ELSE M.MenuPathOnly
          END = @NormalizedPath;
END
GO
