IF OBJECT_ID('dbo.usp_IsUserAuthorizedForPage', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.usp_IsUserAuthorizedForPage;
END
GO

CREATE PROCEDURE dbo.usp_IsUserAuthorizedForPage
    @LoginId NVARCHAR(100),
    @RequestPath NVARCHAR(500),
    @RoleFromQuery NVARCHAR(100) = NULL
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
    SELECT TOP 1
        CAST(1 AS BIT) AS IsAuthorized,
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
      AND
      (
          M.MenuFileName = @NormalizedPath
          OR
          (
              @RoleFromQuery IS NOT NULL
              AND LTRIM(RTRIM(@RoleFromQuery)) <> ''
              AND M.MenuRole = UPPER(LTRIM(RTRIM(@RoleFromQuery)))
              AND
              (
                  M.MenuFileName = @NormalizedPath
                  OR M.MenuFileName = (@NormalizedPath + '?ROLE=' + UPPER(LTRIM(RTRIM(@RoleFromQuery))))
              )
          )
      );

    IF @@ROWCOUNT = 0
    BEGIN
        SELECT CAST(0 AS BIT) AS IsAuthorized;
    END
END
GO
