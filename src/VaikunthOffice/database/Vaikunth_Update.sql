/* =====================================================================
   Vaikunth Office · Vaikunth_Update.sql  (the ONE database script)

   Run this single file to create or update the whole database. It is safe to run again and again:
     - creates every table that is missing (all names start with Vaikunth_),
     - renames tables from the older vk_ scripts to Vaikunth_ (data is kept),
     - adds columns that newer versions need,
     - creates or replaces every stored procedure (Vaikunth_...).

   How to run: open it in SSMS / Azure Data Studio connected to your database and press Execute
   (or: sqlcmd -S <server> -d <database> -i Vaikunth_Update.sql).
   Then set ConnectionStrings:Default in each app's appsettings.json and open /setup once
   to create the super admin.

   All three apps (Akshara Pages, Hrishikesha Workbook, Vishwaroopa Slides) share this one database.
   Every client company is a row in Vaikunth_Tenants; everything a client owns carries its TenantId.

   WHAT IS IN THE DATABASE AND WHAT IS ON THE SERVER
   -------------------------------------------------
   Database: clients, plans, users, passwords (hashed), profile photos, password links, and the LIST of files.
   Server:   the content of every Word, Excel and PowerPoint file, as a file in the folder set in each app's
             appsettings.json  "Storage": { "RootPath": "D:\\VaikunthFiles" }  (empty = App_Data\VaikunthFiles
             in the folder where the app is hosted).

   Vaikunth_Documents keeps one row per file: FileName, who created it and when, who updated it and when,
   IsDeleted, the size, and StoragePath = where the content is, relative to RootPath:
       {client id}/Pages/{file id}.json       Akshara Pages (Word): the page (its HTML) and page size
       {client id}/Workbook/{file id}.json    Hrishikesha Workbook (Excel): sheets → cells (value or =formula + format)
       {client id}/Slides/{file id}.json      Vishwaroopa Slides (PowerPoint): theme, slides → boxes, pictures, notes
   The exact JSON shapes are the classes in Vaikunth.DtoLibrary/DocumentDtos.cs.
   SizeBytes counts towards the client's storage quota.

   Upgrading a database where file content was still inside Vaikunth_Documents.Content: this script moves the
   content into the table Vaikunth_DocumentsToMove and drops the column; each app then writes those files
   to the storage folder the next time it starts, and the table is removed once it is empty.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

/* =========================== 1. old names =========================== */
/* Databases made with the first scripts used vk_ names: rename them, keeping the data. */
IF OBJECT_ID(N'dbo.vk_Plans', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Vaikunth_Plans', N'U') IS NULL
    EXEC sp_rename N'dbo.vk_Plans', N'Vaikunth_Plans';
IF OBJECT_ID(N'dbo.vk_Tenants', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Vaikunth_Tenants', N'U') IS NULL
    EXEC sp_rename N'dbo.vk_Tenants', N'Vaikunth_Tenants';
IF OBJECT_ID(N'dbo.vk_Users', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Vaikunth_Users', N'U') IS NULL
    EXEC sp_rename N'dbo.vk_Users', N'Vaikunth_Users';
IF OBJECT_ID(N'dbo.vk_PasswordLinks', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Vaikunth_PasswordLinks', N'U') IS NULL
    EXEC sp_rename N'dbo.vk_PasswordLinks', N'Vaikunth_PasswordLinks';
IF OBJECT_ID(N'dbo.vk_Documents', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Vaikunth_Documents', N'U') IS NULL
    EXEC sp_rename N'dbo.vk_Documents', N'Vaikunth_Documents';
IF OBJECT_ID(N'dbo.vk_DocumentShares', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Vaikunth_DocumentShares', N'U') IS NULL
    EXEC sp_rename N'dbo.vk_DocumentShares', N'Vaikunth_DocumentShares';
IF OBJECT_ID(N'dbo.vk_DocumentStars', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Vaikunth_DocumentStars', N'U') IS NULL
    EXEC sp_rename N'dbo.vk_DocumentStars', N'Vaikunth_DocumentStars';
GO

/* Older Vaikunth_Documents column names → the new ones (data is kept). */
IF COL_LENGTH(N'dbo.Vaikunth_Documents', N'Title') IS NOT NULL AND COL_LENGTH(N'dbo.Vaikunth_Documents', N'FileName') IS NULL
    EXEC sp_rename N'dbo.Vaikunth_Documents.Title', N'FileName', N'COLUMN';
IF COL_LENGTH(N'dbo.Vaikunth_Documents', N'OwnerId') IS NOT NULL AND COL_LENGTH(N'dbo.Vaikunth_Documents', N'CreatedBy') IS NULL
    EXEC sp_rename N'dbo.Vaikunth_Documents.OwnerId', N'CreatedBy', N'COLUMN';
IF COL_LENGTH(N'dbo.Vaikunth_Documents', N'ModifiedUtc') IS NOT NULL AND COL_LENGTH(N'dbo.Vaikunth_Documents', N'UpdatedUtc') IS NULL
    EXEC sp_rename N'dbo.Vaikunth_Documents.ModifiedUtc', N'UpdatedUtc', N'COLUMN';
IF COL_LENGTH(N'dbo.Vaikunth_Documents', N'ModifiedBy') IS NOT NULL AND COL_LENGTH(N'dbo.Vaikunth_Documents', N'UpdatedBy') IS NULL
    EXEC sp_rename N'dbo.Vaikunth_Documents.ModifiedBy', N'UpdatedBy', N'COLUMN';
GO

/* ============================= 2. tables ============================ */
/* ---------- Plans: the limits a client buys ---------- */
IF OBJECT_ID(N'dbo.Vaikunth_Plans', N'U') IS NULL
CREATE TABLE dbo.Vaikunth_Plans
(
    PlanId          INT              IDENTITY(1,1) NOT NULL CONSTRAINT PK_Vaikunth_Plans PRIMARY KEY,
    Name            NVARCHAR(50)     NOT NULL CONSTRAINT UX_Vaikunth_Plans_Name UNIQUE,
    MaxUsers        INT              NOT NULL,
    StorageQuotaMb  BIGINT           NOT NULL,
    AppsEnabled     TINYINT          NOT NULL CONSTRAINT DF_Vaikunth_Plans_Apps DEFAULT (7),   -- 1 Pages + 2 Workbook + 4 Slides
    PricePerUser    DECIMAL(10, 2)   NOT NULL CONSTRAINT DF_Vaikunth_Plans_Price DEFAULT (0)
);
GO
IF NOT EXISTS (SELECT 1 FROM dbo.Vaikunth_Plans)
    INSERT dbo.Vaikunth_Plans (Name, MaxUsers, StorageQuotaMb, AppsEnabled, PricePerUser) VALUES
        (N'Starter', 10, 1024, 7, 0),
        (N'Business', 50, 10240, 7, 149),
        (N'Enterprise', 500, 102400, 7, 299);
GO

/* ---------- Tenants: one row per client company ---------- */
IF OBJECT_ID(N'dbo.Vaikunth_Tenants', N'U') IS NULL
CREATE TABLE dbo.Vaikunth_Tenants
(
    TenantId                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Vaikunth_Tenants PRIMARY KEY,
    Name                    NVARCHAR(120)    NOT NULL,
    Slug                    NVARCHAR(40)     NOT NULL CONSTRAINT UX_Vaikunth_Tenants_Slug UNIQUE,
    PlanId                  INT              NOT NULL CONSTRAINT FK_Vaikunth_Tenants_Plans REFERENCES dbo.Vaikunth_Plans(PlanId),
    AppsEnabled             TINYINT          NOT NULL CONSTRAINT DF_Vaikunth_Tenants_Apps DEFAULT (7),
    IsActive                BIT              NOT NULL CONSTRAINT DF_Vaikunth_Tenants_Active DEFAULT (1),
    MaxUsersOverride        INT              NULL,
    StorageQuotaMbOverride  BIGINT           NULL,
    StorageUsedBytes        BIGINT           NOT NULL CONSTRAINT DF_Vaikunth_Tenants_Storage DEFAULT (0),
    CreatedUtc              DATETIME2(3)     NOT NULL CONSTRAINT DF_Vaikunth_Tenants_Created DEFAULT (SYSUTCDATETIME())
);
GO

/* ---------- Users (TenantId NULL = Vaikunth Office super admin) ---------- */
IF OBJECT_ID(N'dbo.Vaikunth_Users', N'U') IS NULL
CREATE TABLE dbo.Vaikunth_Users
(
    UserId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Vaikunth_Users PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NULL CONSTRAINT FK_Vaikunth_Users_Tenants REFERENCES dbo.Vaikunth_Tenants(TenantId),
    DisplayName     NVARCHAR(120)    NOT NULL,
    Email           NVARCHAR(200)    NOT NULL,          -- stored lower-case; one person, one email, one client
    PasswordHash    NVARCHAR(400)    NULL,              -- ASP.NET PasswordHasher (PBKDF2), never plain text. NULL = invited, hasn't set a password yet
    Role            NVARCHAR(20)     NOT NULL CONSTRAINT DF_Vaikunth_Users_Role DEFAULT (N'Member'),  -- SuperAdmin | TenantAdmin | Member
    AvatarTone      TINYINT          NOT NULL CONSTRAINT DF_Vaikunth_Users_Tone DEFAULT (0),
    IsActive        BIT              NOT NULL CONSTRAINT DF_Vaikunth_Users_Active DEFAULT (1),
    CreatedUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_Vaikunth_Users_Created DEFAULT (SYSUTCDATETIME()),
    LastLoginUtc    DATETIME2(3)     NULL,
    InvitedBy       UNIQUEIDENTIFIER NULL,
    PasswordSetUtc  DATETIME2(3)     NULL,
    Photo           VARBINARY(MAX)   NULL,              -- profile picture (PNG / JPEG / WebP, at most 2 MB)
    PhotoType       NVARCHAR(50)     NULL,
    PhotoUpdatedUtc DATETIME2(3)     NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Vaikunth_Users_Email')
    CREATE UNIQUE INDEX UX_Vaikunth_Users_Email ON dbo.Vaikunth_Users(Email);
GO

/* ---------- One-time "set your password" links ----------
   Purpose 1 = welcome / invite (valid 7 days), 2 = forgot password (valid 2 hours).
   Only a SHA-256 hash of the link's secret is stored, so a copy of the database can't be used to sign in.
   A new link cancels the person's older unused links. */
IF OBJECT_ID(N'dbo.Vaikunth_PasswordLinks', N'U') IS NULL
CREATE TABLE dbo.Vaikunth_PasswordLinks
(
    TokenHash       BINARY(32)       NOT NULL CONSTRAINT PK_Vaikunth_PasswordLinks PRIMARY KEY,
    UserId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Vaikunth_PasswordLinks_Users REFERENCES dbo.Vaikunth_Users(UserId) ON DELETE CASCADE,
    Purpose         TINYINT          NOT NULL,
    CreatedUtc      DATETIME2(0)     NOT NULL CONSTRAINT DF_Vaikunth_PasswordLinks_Created DEFAULT (SYSUTCDATETIME()),
    ExpiresUtc      DATETIME2(0)     NOT NULL,
    UsedUtc         DATETIME2(0)     NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Vaikunth_PasswordLinks_User')
    CREATE INDEX IX_Vaikunth_PasswordLinks_User ON dbo.Vaikunth_PasswordLinks(UserId, CreatedUtc DESC);
GO

/* ---------- Documents: the LIST of files of all three apps. The content is a file on the server (see the top). ---------- */
IF OBJECT_ID(N'dbo.Vaikunth_Documents', N'U') IS NULL
CREATE TABLE dbo.Vaikunth_Documents
(
    DocumentId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Vaikunth_Documents PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Vaikunth_Documents_Tenants REFERENCES dbo.Vaikunth_Tenants(TenantId),
    AppKind         TINYINT          NOT NULL,          -- 1 Pages (Word) · 2 Workbook (Excel) · 3 Slides (PowerPoint)
    FileName        NVARCHAR(200)    NOT NULL,          -- the name people see, e.g. "Quarterly review"
    StoragePath     NVARCHAR(400)    NOT NULL CONSTRAINT DF_Vaikunth_Documents_Path DEFAULT (N''),  -- relative to Storage:RootPath
    SizeBytes       BIGINT           NOT NULL CONSTRAINT DF_Vaikunth_Documents_Size DEFAULT (0),
    CreatedBy       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Vaikunth_Documents_CreatedBy REFERENCES dbo.Vaikunth_Users(UserId),
    CreatedUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_Vaikunth_Documents_Created DEFAULT (SYSUTCDATETIME()),
    UpdatedBy       UNIQUEIDENTIFIER NULL,
    UpdatedUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_Vaikunth_Documents_Updated DEFAULT (SYSUTCDATETIME()),
    IsDeleted       BIT              NOT NULL CONSTRAINT DF_Vaikunth_Documents_Deleted DEFAULT (0),
    DeletedBy       UNIQUEIDENTIFIER NULL,
    DeletedUtc      DATETIME2(3)     NULL,
    CONSTRAINT CK_Vaikunth_Documents_AppKind CHECK (AppKind IN (1, 2, 3))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Vaikunth_Documents') AND name LIKE N'IX[_]%[_]Documents[_]Owner')
    CREATE INDEX IX_Vaikunth_Documents_Owner ON dbo.Vaikunth_Documents(TenantId, CreatedBy, UpdatedUtc DESC) WHERE IsDeleted = 0;
GO

/* Sharing inside the same client: who else may open or edit a file. CanEdit 0 = view only. */
IF OBJECT_ID(N'dbo.Vaikunth_DocumentShares', N'U') IS NULL
CREATE TABLE dbo.Vaikunth_DocumentShares
(
    DocumentId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Vaikunth_Shares_Doc REFERENCES dbo.Vaikunth_Documents(DocumentId) ON DELETE CASCADE,
    UserId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Vaikunth_Shares_User REFERENCES dbo.Vaikunth_Users(UserId),
    CanEdit         BIT              NOT NULL CONSTRAINT DF_Vaikunth_Shares_CanEdit DEFAULT (0),
    SharedUtc       DATETIME2(3)     NOT NULL CONSTRAINT DF_Vaikunth_Shares_Shared DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Vaikunth_DocumentShares PRIMARY KEY (DocumentId, UserId)
);
GO

/* Stars are per person. */
IF OBJECT_ID(N'dbo.Vaikunth_DocumentStars', N'U') IS NULL
CREATE TABLE dbo.Vaikunth_DocumentStars
(
    DocumentId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Vaikunth_Stars_Doc REFERENCES dbo.Vaikunth_Documents(DocumentId) ON DELETE CASCADE,
    UserId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Vaikunth_Stars_User REFERENCES dbo.Vaikunth_Users(UserId),
    CONSTRAINT PK_Vaikunth_DocumentStars PRIMARY KEY (DocumentId, UserId)
);
GO

/* ========================= 3. newer columns ========================= */
/* Invites and password links (added after the first version). */
IF COL_LENGTH(N'dbo.Vaikunth_Users', N'InvitedBy') IS NULL
    ALTER TABLE dbo.Vaikunth_Users ADD InvitedBy UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Vaikunth_Users', N'PasswordSetUtc') IS NULL
    ALTER TABLE dbo.Vaikunth_Users ADD PasswordSetUtc DATETIME2(3) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Vaikunth_Users') AND name = N'PasswordHash' AND is_nullable = 0)
    ALTER TABLE dbo.Vaikunth_Users ALTER COLUMN PasswordHash NVARCHAR(400) NULL;
/* Profile photos. */
IF COL_LENGTH(N'dbo.Vaikunth_Users', N'Photo') IS NULL
    ALTER TABLE dbo.Vaikunth_Users ADD Photo VARBINARY(MAX) NULL, PhotoType NVARCHAR(50) NULL, PhotoUpdatedUtc DATETIME2(3) NULL;
/* Files on the server: where each file is, and who deleted it. */
IF COL_LENGTH(N'dbo.Vaikunth_Documents', N'StoragePath') IS NULL
    ALTER TABLE dbo.Vaikunth_Documents ADD StoragePath NVARCHAR(400) NOT NULL CONSTRAINT DF_Vaikunth_Documents_Path DEFAULT (N'');
IF COL_LENGTH(N'dbo.Vaikunth_Documents', N'DeletedBy') IS NULL
    ALTER TABLE dbo.Vaikunth_Documents ADD DeletedBy UNIQUEIDENTIFIER NULL, DeletedUtc DATETIME2(3) NULL;
GO

/* File content that is still inside the database moves out: copied to Vaikunth_DocumentsToMove, column dropped.
   Each app writes those files to its storage folder when it starts, then the table is removed. */
IF COL_LENGTH(N'dbo.Vaikunth_Documents', N'Content') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'dbo.Vaikunth_DocumentsToMove', N'U') IS NULL
        CREATE TABLE dbo.Vaikunth_DocumentsToMove (DocumentId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, Content NVARCHAR(MAX) NOT NULL);
    EXEC sys.sp_executesql N'INSERT dbo.Vaikunth_DocumentsToMove (DocumentId, Content)
        SELECT d.DocumentId, d.Content FROM dbo.Vaikunth_Documents d
        WHERE NOT EXISTS (SELECT 1 FROM dbo.Vaikunth_DocumentsToMove m WHERE m.DocumentId = d.DocumentId);';
    DECLARE @df SYSNAME = (SELECT dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Vaikunth_Documents') AND c.name = N'Content');
    IF @df IS NOT NULL EXEC (N'ALTER TABLE dbo.Vaikunth_Documents DROP CONSTRAINT ' + @df);
    EXEC (N'ALTER TABLE dbo.Vaikunth_Documents DROP COLUMN Content');
END
IF OBJECT_ID(N'dbo.Vaikunth_DocumentsToMove', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'IF NOT EXISTS (SELECT 1 FROM dbo.Vaikunth_DocumentsToMove) DROP TABLE dbo.Vaikunth_DocumentsToMove;';
GO

/* ===================== 4. remove old vk_ procedures ================== */
DECLARE @drop NVARCHAR(MAX) = N'';
SELECT @drop += N'DROP PROCEDURE dbo.' + QUOTENAME(name) + N';' + CHAR(10)
FROM sys.procedures WHERE name LIKE N'vk[_]%' AND schema_id = SCHEMA_ID(N'dbo');
IF LEN(@drop) > 0 EXEC sys.sp_executesql @drop;
GO

/* ======================== 5. stored procedures ======================= */
/* Every database call from Vaikunth.ServiceLibrary goes through one of these. */
/* =============================== setup =============================== */
/* 1 when nobody exists yet, so /setup may create the super admin. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_Setup_IsNeeded
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Vaikunth_Users WHERE Role = N'SuperAdmin') THEN 0 ELSE 1 END AS BIT) AS Needed;
END
GO

/* =============================== users =============================== */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_User_GetForLogin @Email NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId, u.TenantId, t.Name AS TenantName, u.DisplayName, u.Email, u.Role, u.AvatarTone, u.CreatedUtc,
           ISNULL(u.PasswordHash, N'') AS PasswordHash, u.IsActive,
           CAST(ISNULL(t.IsActive, 1) AS BIT) AS TenantActive,
           CAST(ISNULL(t.AppsEnabled, 7) AS TINYINT) AS AppsEnabled
    FROM dbo.Vaikunth_Users u
    LEFT JOIN dbo.Vaikunth_Tenants t ON t.TenantId = u.TenantId
    WHERE u.Email = LOWER(@Email);
END
GO

CREATE OR ALTER PROCEDURE dbo.Vaikunth_User_Get @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId, u.TenantId, t.Name AS TenantName, u.DisplayName, u.Email, u.Role, u.AvatarTone, u.CreatedUtc,
           CAST(ISNULL(t.AppsEnabled, 7) AS TINYINT) AS AppsEnabled
    FROM dbo.Vaikunth_Users u LEFT JOIN dbo.Vaikunth_Tenants t ON t.TenantId = u.TenantId
    WHERE u.UserId = @UserId;
END
GO

CREATE OR ALTER PROCEDURE dbo.Vaikunth_User_TouchLogin @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Vaikunth_Users SET LastLoginUtc = SYSUTCDATETIME() WHERE UserId = @UserId;
END
GO

/* Everyone in one client, for the client admin's People page. Status: Invited (no password yet), Active, Off. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_User_ListForTenant @TenantId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId, u.DisplayName, u.Email, u.Role, u.AvatarTone, u.IsActive, u.CreatedUtc, u.LastLoginUtc,
           CAST(CASE WHEN u.PasswordHash IS NULL THEN 1 ELSE 0 END AS BIT) AS IsInvited,
           i.DisplayName AS InvitedByName,
           CASE WHEN u.Photo IS NULL THEN NULL ELSE u.PhotoUpdatedUtc END AS PhotoUpdatedUtc
    FROM dbo.Vaikunth_Users u
    LEFT JOIN dbo.Vaikunth_Users i ON i.UserId = u.InvitedBy
    WHERE u.TenantId = @TenantId
    ORDER BY CASE u.Role WHEN N'TenantAdmin' THEN 0 ELSE 1 END, u.DisplayName;
END
GO

/* ---------- My account: a person changes their own name, email and password ---------- */
/* Result 1 = saved, 0 = the email belongs to someone else. @PasswordHash NULL keeps the password.
   A new password also cancels any open password links. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_User_UpdateAccount
    @UserId UNIQUEIDENTIFIER, @DisplayName NVARCHAR(120), @Email NVARCHAR(200), @PasswordHash NVARCHAR(400) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @Email = LOWER(LTRIM(RTRIM(@Email)));
    IF EXISTS (SELECT 1 FROM dbo.Vaikunth_Users WHERE Email = @Email AND UserId <> @UserId) BEGIN SELECT 0 AS Result; RETURN; END
    BEGIN TRAN;
        UPDATE dbo.Vaikunth_Users
        SET DisplayName = @DisplayName, Email = @Email,
            PasswordHash = ISNULL(@PasswordHash, PasswordHash),
            PasswordSetUtc = CASE WHEN @PasswordHash IS NULL THEN PasswordSetUtc ELSE SYSUTCDATETIME() END
        WHERE UserId = @UserId;
        IF @@ROWCOUNT = 0 BEGIN ROLLBACK; SELECT -1 AS Result; RETURN; END
        IF @PasswordHash IS NOT NULL
            UPDATE dbo.Vaikunth_PasswordLinks SET UsedUtc = SYSUTCDATETIME() WHERE UserId = @UserId AND UsedUtc IS NULL;
    COMMIT;
    SELECT 1 AS Result;
END
GO

/* ---------- profile photos (kept in the database with the person) ---------- */
/* A person's photo, only for people of the same client (or the super admin). */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_User_GetPhoto @UserId UNIQUEIDENTIFIER, @ViewerTenantId UNIQUEIDENTIFIER = NULL, @ViewerIsSuperAdmin BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.Photo, u.PhotoType, u.PhotoUpdatedUtc
    FROM dbo.Vaikunth_Users u
    WHERE u.UserId = @UserId AND u.Photo IS NOT NULL
      AND (@ViewerIsSuperAdmin = 1 OR u.TenantId = @ViewerTenantId OR (u.TenantId IS NULL AND @ViewerTenantId IS NULL));
END
GO

/* When the photo last changed; nothing / NULL = no photo. Read at sign-in. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_User_GetPhotoVersion @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN Photo IS NULL THEN NULL ELSE PhotoUpdatedUtc END AS PhotoUpdatedUtc FROM dbo.Vaikunth_Users WHERE UserId = @UserId;
END
GO

/* Saves the photo; @Photo NULL removes it. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_User_SetPhoto @UserId UNIQUEIDENTIFIER, @Photo VARBINARY(MAX) = NULL, @PhotoType NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Vaikunth_Users
    SET Photo = @Photo, PhotoType = CASE WHEN @Photo IS NULL THEN NULL ELSE @PhotoType END,
        PhotoUpdatedUtc = CASE WHEN @Photo IS NULL THEN NULL ELSE SYSUTCDATETIME() END
    WHERE UserId = @UserId;
    SELECT PhotoUpdatedUtc FROM dbo.Vaikunth_Users WHERE UserId = @UserId;
END
GO

/* For "Forgot password?": the person behind an email, if any. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_User_GetByEmail @Email NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId, u.TenantId, t.Name AS TenantName, u.DisplayName, u.Email, u.Role, u.IsActive,
           CAST(ISNULL(t.IsActive, 1) AS BIT) AS TenantActive,
           CAST(CASE WHEN u.PasswordHash IS NULL THEN 1 ELSE 0 END AS BIT) AS IsInvited
    FROM dbo.Vaikunth_Users u LEFT JOIN dbo.Vaikunth_Tenants t ON t.TenantId = u.TenantId
    WHERE u.Email = LOWER(@Email);
END
GO

/* A client admin adds a person with no password; they get an emailed link to set one.
   Result: 1 added · 0 email already used · -1 the client has reached its user limit. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_User_Invite
    @UserId UNIQUEIDENTIFIER, @TenantId UNIQUEIDENTIFIER, @DisplayName NVARCHAR(120), @Email NVARCHAR(200),
    @Role NVARCHAR(20), @AvatarTone TINYINT, @InvitedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET @Email = LOWER(@Email);
    IF EXISTS (SELECT 1 FROM dbo.Vaikunth_Users WHERE Email = @Email) BEGIN SELECT 0 AS Result; RETURN; END
    IF (SELECT COUNT(*) FROM dbo.Vaikunth_Users WHERE TenantId = @TenantId AND IsActive = 1) >=
       (SELECT ISNULL(t.MaxUsersOverride, p.MaxUsers) FROM dbo.Vaikunth_Tenants t JOIN dbo.Vaikunth_Plans p ON p.PlanId = t.PlanId WHERE t.TenantId = @TenantId)
    BEGIN SELECT -1 AS Result; RETURN; END
    INSERT dbo.Vaikunth_Users (UserId, TenantId, DisplayName, Email, PasswordHash, Role, AvatarTone, InvitedBy)
    VALUES (@UserId, @TenantId, @DisplayName, @Email, NULL, CASE WHEN @Role = N'TenantAdmin' THEN N'TenantAdmin' ELSE N'Member' END, @AvatarTone, @InvitedBy);
    SELECT 1 AS Result;
END
GO

/* Only inside the admin's own client; never the super admin. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_User_SetActive @TenantId UNIQUEIDENTIFIER, @UserId UNIQUEIDENTIFIER, @IsActive BIT
AS
BEGIN
    UPDATE dbo.Vaikunth_Users SET IsActive = @IsActive WHERE UserId = @UserId AND TenantId = @TenantId;
END
GO

CREATE OR ALTER PROCEDURE dbo.Vaikunth_User_SetRole @TenantId UNIQUEIDENTIFIER, @UserId UNIQUEIDENTIFIER, @Role NVARCHAR(20)
AS
BEGIN
    IF @Role NOT IN (N'TenantAdmin', N'Member') RETURN;
    UPDATE dbo.Vaikunth_Users SET Role = @Role WHERE UserId = @UserId AND TenantId = @TenantId;
END
GO

/* ============================ password links ============================ */
/* Result: 0 created · 5 a link was made less than @MinGapSeconds ago (stops an inbox being flooded). */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_PasswordLink_Create
    @UserId UNIQUEIDENTIFIER, @TokenHash BINARY(32), @Purpose TINYINT, @ExpiresUtc DATETIME2(0), @MinGapSeconds INT = 60
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    IF EXISTS (SELECT 1 FROM dbo.Vaikunth_PasswordLinks WITH (UPDLOCK, HOLDLOCK)
               WHERE UserId = @UserId AND CreatedUtc > DATEADD(SECOND, -@MinGapSeconds, SYSUTCDATETIME()))
    BEGIN ROLLBACK; SELECT 5 AS Result; RETURN; END
    UPDATE dbo.Vaikunth_PasswordLinks SET UsedUtc = SYSUTCDATETIME() WHERE UserId = @UserId AND UsedUtc IS NULL;
    INSERT dbo.Vaikunth_PasswordLinks (TokenHash, UserId, Purpose, ExpiresUtc) VALUES (@TokenHash, @UserId, @Purpose, @ExpiresUtc);
    DELETE dbo.Vaikunth_PasswordLinks WHERE ExpiresUtc < DATEADD(DAY, -30, SYSUTCDATETIME());
    COMMIT;
    SELECT 0 AS Result;
END
GO

CREATE OR ALTER PROCEDURE dbo.Vaikunth_PasswordLink_Get @TokenHash BINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT l.Purpose, l.ExpiresUtc, l.UsedUtc, u.DisplayName, u.Email, u.IsActive,
           t.Name AS TenantName, CAST(ISNULL(t.IsActive, 1) AS BIT) AS TenantActive
    FROM dbo.Vaikunth_PasswordLinks l
    JOIN dbo.Vaikunth_Users u ON u.UserId = l.UserId
    LEFT JOIN dbo.Vaikunth_Tenants t ON t.TenantId = u.TenantId
    WHERE l.TokenHash = @TokenHash;
END
GO

/* Sets the password from a link. Result: 0 done · 1 link not valid (wrong, used or expired) · 2 account or client switched off. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_PasswordLink_Use @TokenHash BINARY(32), @PasswordHash NVARCHAR(400)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @UserId UNIQUEIDENTIFIER, @Active BIT, @TenantActive BIT;
    BEGIN TRAN;
    SELECT @UserId = u.UserId, @Active = u.IsActive, @TenantActive = CAST(ISNULL(t.IsActive, 1) AS BIT)
    FROM dbo.Vaikunth_PasswordLinks l WITH (UPDLOCK)
    JOIN dbo.Vaikunth_Users u ON u.UserId = l.UserId
    LEFT JOIN dbo.Vaikunth_Tenants t ON t.TenantId = u.TenantId
    WHERE l.TokenHash = @TokenHash AND l.UsedUtc IS NULL AND l.ExpiresUtc > SYSUTCDATETIME();
    IF @UserId IS NULL BEGIN ROLLBACK; SELECT 1 AS Result; RETURN; END
    IF @Active = 0 OR @TenantActive = 0 BEGIN ROLLBACK; SELECT 2 AS Result; RETURN; END
    UPDATE dbo.Vaikunth_Users SET PasswordHash = @PasswordHash, PasswordSetUtc = SYSUTCDATETIME() WHERE UserId = @UserId;
    UPDATE dbo.Vaikunth_PasswordLinks SET UsedUtc = SYSUTCDATETIME() WHERE UserId = @UserId AND UsedUtc IS NULL;
    COMMIT;
    SELECT 0 AS Result, @UserId AS UserId;
END
GO

/* Result: 1 created · 0 email already used · -1 the client has reached its user limit · -2 super admin already exists. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_User_Create
    @UserId UNIQUEIDENTIFIER, @TenantId UNIQUEIDENTIFIER = NULL, @DisplayName NVARCHAR(120), @Email NVARCHAR(200),
    @PasswordHash NVARCHAR(400), @Role NVARCHAR(20), @AvatarTone TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Email = LOWER(@Email);
    IF EXISTS (SELECT 1 FROM dbo.Vaikunth_Users WHERE Email = @Email) BEGIN SELECT 0 AS Result; RETURN; END
    IF @Role = N'SuperAdmin' AND EXISTS (SELECT 1 FROM dbo.Vaikunth_Users WHERE Role = N'SuperAdmin') BEGIN SELECT -2 AS Result; RETURN; END
    IF @TenantId IS NOT NULL AND
       (SELECT COUNT(*) FROM dbo.Vaikunth_Users WHERE TenantId = @TenantId AND IsActive = 1) >=
       (SELECT ISNULL(t.MaxUsersOverride, p.MaxUsers) FROM dbo.Vaikunth_Tenants t JOIN dbo.Vaikunth_Plans p ON p.PlanId = t.PlanId WHERE t.TenantId = @TenantId)
    BEGIN SELECT -1 AS Result; RETURN; END

    INSERT dbo.Vaikunth_Users (UserId, TenantId, DisplayName, Email, PasswordHash, Role, AvatarTone)
    VALUES (@UserId, @TenantId, @DisplayName, @Email, @PasswordHash, @Role, @AvatarTone);
    SELECT 1 AS Result;
END
GO

/* =============================== plans and clients (super admin) =============================== */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_Plan_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PlanId, Name, MaxUsers, StorageQuotaMb, AppsEnabled, PricePerUser FROM dbo.Vaikunth_Plans ORDER BY MaxUsers;
END
GO

/* Every client with its limits (overrides applied), usage and file counts. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_Tenant_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.TenantId, t.Name, t.Slug, t.PlanId, p.Name AS PlanName, t.IsActive, t.CreatedUtc,
           ISNULL(t.MaxUsersOverride, p.MaxUsers) AS MaxUsers,
           (SELECT COUNT(*) FROM dbo.Vaikunth_Users u WHERE u.TenantId = t.TenantId AND u.IsActive = 1) AS ActiveUsers,
           CAST(ISNULL(t.StorageQuotaMbOverride, p.StorageQuotaMb) AS BIGINT) * 1048576 AS StorageQuotaBytes,
           t.StorageUsedBytes, t.AppsEnabled,
           (SELECT COUNT(*) FROM dbo.Vaikunth_Documents d WHERE d.TenantId = t.TenantId AND d.IsDeleted = 0 AND d.AppKind = 1) AS Documents,
           (SELECT COUNT(*) FROM dbo.Vaikunth_Documents d WHERE d.TenantId = t.TenantId AND d.IsDeleted = 0 AND d.AppKind = 2) AS Workbooks,
           (SELECT COUNT(*) FROM dbo.Vaikunth_Documents d WHERE d.TenantId = t.TenantId AND d.IsDeleted = 0 AND d.AppKind = 3) AS Decks,
           a.Email AS AdminEmail, a.UserId AS AdminUserId,
           CAST(CASE WHEN a.UserId IS NOT NULL AND a.PasswordHash IS NULL THEN 1 ELSE 0 END AS BIT) AS AdminPending
    FROM dbo.Vaikunth_Tenants t
    JOIN dbo.Vaikunth_Plans p ON p.PlanId = t.PlanId
    OUTER APPLY (SELECT TOP 1 u.UserId, u.Email, u.PasswordHash FROM dbo.Vaikunth_Users u
                 WHERE u.TenantId = t.TenantId AND u.Role = N'TenantAdmin' ORDER BY u.CreatedUtc) a
    ORDER BY t.Name;
END
GO

CREATE OR ALTER PROCEDURE dbo.Vaikunth_Suite_Stats
AS
BEGIN
    SET NOCOUNT ON;
    SELECT (SELECT COUNT(*) FROM dbo.Vaikunth_Tenants) AS Clients,
           (SELECT COUNT(*) FROM dbo.Vaikunth_Tenants WHERE IsActive = 1) AS ActiveClients,
           (SELECT COUNT(*) FROM dbo.Vaikunth_Users WHERE TenantId IS NOT NULL AND IsActive = 1) AS Users,
           (SELECT COUNT(*) FROM dbo.Vaikunth_Documents WHERE IsDeleted = 0 AND AppKind = 1) AS Documents,
           (SELECT COUNT(*) FROM dbo.Vaikunth_Documents WHERE IsDeleted = 0 AND AppKind = 2) AS Workbooks,
           (SELECT COUNT(*) FROM dbo.Vaikunth_Documents WHERE IsDeleted = 0 AND AppKind = 3) AS Decks,
           (SELECT ISNULL(SUM(StorageUsedBytes), 0) FROM dbo.Vaikunth_Tenants) AS StorageUsedBytes;
END
GO

/* Creates the client and its first admin together (the admin has no password yet when @AdminPasswordHash is NULL). Result: 1 ok · 0 slug taken · -1 admin email taken. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_Tenant_Create
    @TenantId UNIQUEIDENTIFIER, @Name NVARCHAR(120), @Slug NVARCHAR(40), @PlanId INT, @AppsEnabled TINYINT,
    @AdminUserId UNIQUEIDENTIFIER, @AdminName NVARCHAR(120), @AdminEmail NVARCHAR(200),
    @AdminPasswordHash NVARCHAR(400) = NULL,   -- NULL = the admin sets their own password from the welcome email
    @AdminTone TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @Slug = LOWER(@Slug); SET @AdminEmail = LOWER(@AdminEmail);
    IF EXISTS (SELECT 1 FROM dbo.Vaikunth_Tenants WHERE Slug = @Slug) BEGIN SELECT 0 AS Result; RETURN; END
    IF EXISTS (SELECT 1 FROM dbo.Vaikunth_Users WHERE Email = @AdminEmail) BEGIN SELECT -1 AS Result; RETURN; END

    BEGIN TRAN;
        INSERT dbo.Vaikunth_Tenants (TenantId, Name, Slug, PlanId, AppsEnabled) VALUES (@TenantId, @Name, @Slug, @PlanId, @AppsEnabled);
        INSERT dbo.Vaikunth_Users (UserId, TenantId, DisplayName, Email, PasswordHash, Role, AvatarTone)
        VALUES (@AdminUserId, @TenantId, @AdminName, @AdminEmail, @AdminPasswordHash, N'TenantAdmin', @AdminTone);
    COMMIT;
    SELECT 1 AS Result;
END
GO

CREATE OR ALTER PROCEDURE dbo.Vaikunth_Tenant_Update
    @TenantId UNIQUEIDENTIFIER, @Name NVARCHAR(120), @PlanId INT, @AppsEnabled TINYINT,
    @MaxUsersOverride INT = NULL, @StorageQuotaMbOverride BIGINT = NULL, @IsActive BIT
AS
BEGIN
    UPDATE dbo.Vaikunth_Tenants
    SET Name = @Name, PlanId = @PlanId, AppsEnabled = @AppsEnabled,
        MaxUsersOverride = @MaxUsersOverride, StorageQuotaMbOverride = @StorageQuotaMbOverride, IsActive = @IsActive
    WHERE TenantId = @TenantId;
END
GO

/* Switching a client off stops all its people signing in (checked at login). */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_Tenant_SetActive @TenantId UNIQUEIDENTIFIER, @IsActive BIT
AS
BEGIN
    UPDATE dbo.Vaikunth_Tenants SET IsActive = @IsActive WHERE TenantId = @TenantId;
END
GO

/* =============================== documents =============================== */
/* Files the person owns or was shared, newest first. @AppKind NULL = all three apps (the launcher). */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_Document_Recent @TenantId UNIQUEIDENTIFIER, @UserId UNIQUEIDENTIFIER, @AppKind TINYINT = NULL, @Take INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Take) d.DocumentId, d.AppKind, d.FileName, u.DisplayName AS CreatedByName, d.UpdatedUtc,
           CAST(CASE WHEN st.UserId IS NULL THEN 0 ELSE 1 END AS BIT) AS IsStarred
    FROM dbo.Vaikunth_Documents d
    JOIN dbo.Vaikunth_Users u ON u.UserId = d.CreatedBy
    LEFT JOIN dbo.Vaikunth_DocumentStars st ON st.DocumentId = d.DocumentId AND st.UserId = @UserId
    WHERE d.TenantId = @TenantId AND d.IsDeleted = 0
      AND (@AppKind IS NULL OR d.AppKind = @AppKind)
      AND (d.CreatedBy = @UserId OR EXISTS (SELECT 1 FROM dbo.Vaikunth_DocumentShares s WHERE s.DocumentId = d.DocumentId AND s.UserId = @UserId))
    ORDER BY d.UpdatedUtc DESC;
END
GO

/* One file's details and where its content is on the server (StoragePath). */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_Document_Get @TenantId UNIQUEIDENTIFIER, @UserId UNIQUEIDENTIFIER, @DocumentId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT d.DocumentId, d.AppKind, d.FileName, d.StoragePath, d.CreatedBy, u.DisplayName AS CreatedByName, d.CreatedUtc, d.UpdatedUtc,
           CAST(CASE WHEN st.UserId IS NULL THEN 0 ELSE 1 END AS BIT) AS IsStarred
    FROM dbo.Vaikunth_Documents d
    JOIN dbo.Vaikunth_Users u ON u.UserId = d.CreatedBy
    LEFT JOIN dbo.Vaikunth_DocumentStars st ON st.DocumentId = d.DocumentId AND st.UserId = @UserId
    WHERE d.DocumentId = @DocumentId AND d.TenantId = @TenantId AND d.IsDeleted = 0
      AND (d.CreatedBy = @UserId OR EXISTS (SELECT 1 FROM dbo.Vaikunth_DocumentShares s WHERE s.DocumentId = d.DocumentId AND s.UserId = @UserId));
END
GO

/* Records a save. The app has already written the content to a temporary file; it puts the file in place only when
   this returns Result 1. No row = the person may not edit it; Result -1 = the client's storage is full.
   Keeps Vaikunth_Tenants.StorageUsedBytes in step. Returns the StoragePath to use. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_Document_Save
    @DocumentId UNIQUEIDENTIFIER, @IsNew BIT, @TenantId UNIQUEIDENTIFIER, @UserId UNIQUEIDENTIFIER, @AppKind TINYINT,
    @FileName NVARCHAR(200), @StoragePath NVARCHAR(400), @SizeBytes BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Old BIGINT = 0, @Quota BIGINT, @Used BIGINT, @Path NVARCHAR(400) = @StoragePath;
    SELECT @Quota = CAST(ISNULL(t.StorageQuotaMbOverride, p.StorageQuotaMb) AS BIGINT) * 1048576, @Used = t.StorageUsedBytes
    FROM dbo.Vaikunth_Tenants t JOIN dbo.Vaikunth_Plans p ON p.PlanId = t.PlanId WHERE t.TenantId = @TenantId AND t.IsActive = 1;
    IF @Quota IS NULL RETURN;   -- client missing or switched off
    IF @IsNew = 0
        SELECT @Old = SizeBytes, @Path = CASE WHEN StoragePath = N'' THEN @StoragePath ELSE StoragePath END
        FROM dbo.Vaikunth_Documents WHERE DocumentId = @DocumentId AND TenantId = @TenantId;
    IF @Used - @Old + @SizeBytes > @Quota BEGIN SELECT -1 AS Result, @Path AS StoragePath; RETURN; END

    BEGIN TRAN;
    IF @IsNew = 1
        INSERT dbo.Vaikunth_Documents (DocumentId, TenantId, AppKind, FileName, StoragePath, SizeBytes, CreatedBy, UpdatedBy)
        VALUES (@DocumentId, @TenantId, @AppKind, @FileName, @Path, @SizeBytes, @UserId, @UserId);
    ELSE
    BEGIN
        UPDATE d SET FileName = @FileName, StoragePath = @Path, SizeBytes = @SizeBytes, UpdatedUtc = SYSUTCDATETIME(), UpdatedBy = @UserId
        FROM dbo.Vaikunth_Documents d
        WHERE d.DocumentId = @DocumentId AND d.TenantId = @TenantId AND d.IsDeleted = 0
          AND (d.CreatedBy = @UserId OR EXISTS (SELECT 1 FROM dbo.Vaikunth_DocumentShares s WHERE s.DocumentId = d.DocumentId AND s.UserId = @UserId AND s.CanEdit = 1));
        IF @@ROWCOUNT = 0 BEGIN ROLLBACK; RETURN; END
    END
    UPDATE dbo.Vaikunth_Tenants SET StorageUsedBytes = StorageUsedBytes - @Old + @SizeBytes WHERE TenantId = @TenantId;
    COMMIT;
    SELECT 1 AS Result, @Path AS StoragePath;
END
GO

CREATE OR ALTER PROCEDURE dbo.Vaikunth_Document_SetStarred @UserId UNIQUEIDENTIFIER, @DocumentId UNIQUEIDENTIFIER, @IsStarred BIT
AS
BEGIN
    IF @IsStarred = 1 AND NOT EXISTS (SELECT 1 FROM dbo.Vaikunth_DocumentStars WHERE DocumentId = @DocumentId AND UserId = @UserId)
        INSERT dbo.Vaikunth_DocumentStars (DocumentId, UserId) VALUES (@DocumentId, @UserId);
    ELSE IF @IsStarred = 0
        DELETE dbo.Vaikunth_DocumentStars WHERE DocumentId = @DocumentId AND UserId = @UserId;
END
GO

/* Soft delete; only the person who created it. IsDeleted = 1, the file stays on the server. Frees the client's storage. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_Document_Delete @TenantId UNIQUEIDENTIFIER, @UserId UNIQUEIDENTIFIER, @DocumentId UNIQUEIDENTIFIER
AS
BEGIN
    SET XACT_ABORT ON;
    DECLARE @Size BIGINT;
    SELECT @Size = SizeBytes FROM dbo.Vaikunth_Documents WHERE DocumentId = @DocumentId AND TenantId = @TenantId AND CreatedBy = @UserId AND IsDeleted = 0;
    IF @Size IS NULL RETURN;
    BEGIN TRAN;
        UPDATE dbo.Vaikunth_Documents SET IsDeleted = 1, DeletedBy = @UserId, DeletedUtc = SYSUTCDATETIME() WHERE DocumentId = @DocumentId;
        UPDATE dbo.Vaikunth_Tenants SET StorageUsedBytes = StorageUsedBytes - @Size WHERE TenantId = @TenantId;
    COMMIT;
END
GO

/* ---------- moving old content out of the database (only after an upgrade) ---------- */
/* The next files of one app still waiting in Vaikunth_DocumentsToMove. Nothing when there is nothing to move. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_DocumentMove_Next @AppKind TINYINT, @Take INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    IF OBJECT_ID(N'dbo.Vaikunth_DocumentsToMove', N'U') IS NULL RETURN;
    EXEC sys.sp_executesql N'SELECT TOP (@Take) d.DocumentId, d.TenantId, d.AppKind, m.Content
        FROM dbo.Vaikunth_DocumentsToMove m JOIN dbo.Vaikunth_Documents d ON d.DocumentId = m.DocumentId
        WHERE d.AppKind = @AppKind;', N'@AppKind TINYINT, @Take INT', @AppKind, @Take;
END
GO

/* One moved file is now on the server: record where, fix its size (the client's storage too), remove the copy. */
CREATE OR ALTER PROCEDURE dbo.Vaikunth_DocumentMove_Done @DocumentId UNIQUEIDENTIFIER, @StoragePath NVARCHAR(400), @SizeBytes BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF OBJECT_ID(N'dbo.Vaikunth_DocumentsToMove', N'U') IS NULL RETURN;
    DECLARE @Old BIGINT, @TenantId UNIQUEIDENTIFIER, @IsDeleted BIT;
    SELECT @Old = SizeBytes, @TenantId = TenantId, @IsDeleted = IsDeleted FROM dbo.Vaikunth_Documents WHERE DocumentId = @DocumentId;
    BEGIN TRAN;
        UPDATE dbo.Vaikunth_Documents SET StoragePath = @StoragePath, SizeBytes = @SizeBytes WHERE DocumentId = @DocumentId;
        IF @IsDeleted = 0 UPDATE dbo.Vaikunth_Tenants SET StorageUsedBytes = StorageUsedBytes - @Old + @SizeBytes WHERE TenantId = @TenantId;
        EXEC sys.sp_executesql N'DELETE dbo.Vaikunth_DocumentsToMove WHERE DocumentId = @Id;', N'@Id UNIQUEIDENTIFIER', @DocumentId;
    COMMIT;
END
IF OBJECT_ID(N'dbo.Vaikunth_DocumentsToMove', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'IF NOT EXISTS (SELECT 1 FROM dbo.Vaikunth_DocumentsToMove) DROP TABLE dbo.Vaikunth_DocumentsToMove;';
GO

PRINT N'Vaikunth Office database is up to date.';
GO
