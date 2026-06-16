/*
Safe upgrade path for permission tables.

What this script does:
1. Renames the old dbo.RoleMenus table to dbo.RoleMenus_Legacy if it still uses MenuCode.
2. Creates the new normalized tables:
   - dbo.Menus
   - dbo.Permissions
   - dbo.RoleMenus
   - dbo.RolePermissions
3. Copies data from the old schema into the new schema.

Run this once in SQL Server Management Studio after backing up the database.
*/

SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    -------------------------------------------------------------------------
    -- Step 1: rename the legacy RoleMenus table if needed
    -------------------------------------------------------------------------
    IF OBJECT_ID('dbo.RoleMenus', 'U') IS NOT NULL
       AND COL_LENGTH('dbo.RoleMenus', 'MenuId') IS NULL
       AND COL_LENGTH('dbo.RoleMenus', 'MenuCode') IS NOT NULL
       AND OBJECT_ID('dbo.RoleMenus_Legacy', 'U') IS NULL
    BEGIN
        EXEC sp_rename 'dbo.RoleMenus', 'RoleMenus_Legacy';
    END

    -------------------------------------------------------------------------
    -- Step 2: create the new tables if they do not already exist
    -------------------------------------------------------------------------
    IF OBJECT_ID('dbo.Menus', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Menus (
            MenuId        bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Menus PRIMARY KEY,
            MenuCode      nvarchar(50)  NOT NULL,
            ParentMenuId  bigint        NULL,
            MenuName      nvarchar(100) NOT NULL,
            MenuUrl       nvarchar(200) NULL,
            Icon          nvarchar(50)  NULL,
            Visible       bit           NOT NULL CONSTRAINT DF_Menus_Visible DEFAULT (1),
            OrderNo       int           NOT NULL CONSTRAINT DF_Menus_OrderNo DEFAULT (0),
            IsDeleted     bit           NOT NULL CONSTRAINT DF_Menus_IsDeleted DEFAULT (0),
            CreatedById   bigint        NULL,
            CreatedAt     datetime2(0)  NOT NULL CONSTRAINT DF_Menus_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedById   bigint        NULL,
            UpdatedAt     datetime2(0)  NULL,
            CONSTRAINT UQ_Menus_MenuCode UNIQUE (MenuCode)
        );

        ALTER TABLE dbo.Menus
            ADD CONSTRAINT FK_Menus_ParentMenu
            FOREIGN KEY (ParentMenuId) REFERENCES dbo.Menus(MenuId);

        CREATE INDEX IX_Menus_ParentMenuId ON dbo.Menus(ParentMenuId);
    END

    IF OBJECT_ID('dbo.Permissions', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Permissions (
            PermissionId    bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Permissions PRIMARY KEY,
            PermissionCode  nvarchar(50)  NOT NULL,
            MenuId          bigint        NOT NULL,
            ActionName      nvarchar(100) NOT NULL,
            ApiName         nvarchar(100) NOT NULL,
            HttpMethod      nvarchar(20)  NULL,
            Visible         bit           NOT NULL CONSTRAINT DF_Permissions_Visible DEFAULT (1),
            OrderNo         int           NOT NULL CONSTRAINT DF_Permissions_OrderNo DEFAULT (0),
            IsDeleted       bit           NOT NULL CONSTRAINT DF_Permissions_IsDeleted DEFAULT (0),
            CreatedById     bigint        NULL,
            CreatedAt       datetime2(0)  NOT NULL CONSTRAINT DF_Permissions_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedById     bigint        NULL,
            UpdatedAt       datetime2(0)  NULL,
            CONSTRAINT UQ_Permissions_PermissionCode UNIQUE (PermissionCode)
        );

        ALTER TABLE dbo.Permissions
            ADD CONSTRAINT FK_Permissions_Menu
            FOREIGN KEY (MenuId) REFERENCES dbo.Menus(MenuId);

        CREATE INDEX IX_Permissions_MenuId ON dbo.Permissions(MenuId);
    END

    IF OBJECT_ID('dbo.RoleMenus', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RoleMenus (
            RoleMenuId   bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RoleMenus PRIMARY KEY,
            RoleId       bigint       NOT NULL,
            MenuId       bigint       NOT NULL,
            IsDeleted    bit          NOT NULL CONSTRAINT DF_RoleMenus_IsDeleted DEFAULT (0),
            CreatedById  bigint       NULL,
            CreatedAt    datetime2(0) NOT NULL CONSTRAINT DF_RoleMenus_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedById  bigint       NULL,
            UpdatedAt    datetime2(0) NULL,
            CONSTRAINT UQ_RoleMenus_Role_Menu UNIQUE (RoleId, MenuId)
        );

        ALTER TABLE dbo.RoleMenus
            ADD CONSTRAINT FK_RoleMenus_Roles
            FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id);

        ALTER TABLE dbo.RoleMenus
            ADD CONSTRAINT FK_RoleMenus_Menus
            FOREIGN KEY (MenuId) REFERENCES dbo.Menus(MenuId);

        CREATE INDEX IX_RoleMenus_RoleId ON dbo.RoleMenus(RoleId);
        CREATE INDEX IX_RoleMenus_MenuId ON dbo.RoleMenus(MenuId);
    END

    IF OBJECT_ID('dbo.RolePermissions', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RolePermissions (
            RolePermissionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RolePermissions PRIMARY KEY,
            RoleId           bigint       NOT NULL,
            PermissionId     bigint       NOT NULL,
            IsDeleted        bit          NOT NULL CONSTRAINT DF_RolePermissions_IsDeleted DEFAULT (0),
            CreatedById      bigint       NULL,
            CreatedAt        datetime2(0) NOT NULL CONSTRAINT DF_RolePermissions_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedById      bigint       NULL,
            UpdatedAt        datetime2(0) NULL,
            CONSTRAINT UQ_RolePermissions_Role_Permission UNIQUE (RoleId, PermissionId)
        );

        ALTER TABLE dbo.RolePermissions
            ADD CONSTRAINT FK_RolePermissions_Roles
            FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id);

        ALTER TABLE dbo.RolePermissions
            ADD CONSTRAINT FK_RolePermissions_Permissions
            FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions(PermissionId);

        CREATE INDEX IX_RolePermissions_RoleId ON dbo.RolePermissions(RoleId);
        CREATE INDEX IX_RolePermissions_PermissionId ON dbo.RolePermissions(PermissionId);
    END

    -------------------------------------------------------------------------
    -- Step 3: build mapping tables from the old source tables
    -------------------------------------------------------------------------
    IF OBJECT_ID('tempdb..#MenuStage') IS NOT NULL DROP TABLE #MenuStage;
    IF OBJECT_ID('tempdb..#PermissionStage') IS NOT NULL DROP TABLE #PermissionStage;

    CREATE TABLE #MenuStage (
        OldMenuCode   nvarchar(50) NOT NULL PRIMARY KEY,
        OldParentCode nvarchar(50) NULL,
        NewMenuId     bigint NOT NULL
    );

    CREATE TABLE #PermissionStage (
        OldPermissionCode nvarchar(50) NOT NULL PRIMARY KEY,
        OldParentCode     nvarchar(50) NOT NULL,
        NewPermissionId   bigint NOT NULL
    );

    -------------------------------------------------------------------------
    -- Step 4: copy menus
    -------------------------------------------------------------------------
    IF OBJECT_ID('dbo.Menus', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Menus)
    BEGIN
        INSERT INTO dbo.Menus (
            MenuCode, ParentMenuId, MenuName, MenuUrl, Icon,
            Visible, OrderNo, IsDeleted,
            CreatedById, CreatedAt, UpdatedById, UpdatedAt
        )
        OUTPUT
            inserted.MenuCode,
            inserted.MenuId
        INTO #MenuStage (OldMenuCode, NewMenuId)
        SELECT
            src.MenuCode,
            NULL,
            src.MenuName,
            src.MenuUrl,
            src.Icon,
            src.Visible,
            src.OrderNo,
            CASE WHEN src.DelFlag = 0 THEN 0 ELSE 1 END,
            TRY_CONVERT(bigint, src.CreatedUserId),
            src.CreatedDateTime,
            TRY_CONVERT(bigint, src.ModifiedUserId),
            src.ModifiedDateTime
        FROM dbo.MenuAdmins src
        WHERE src.DelFlag = 0;

        UPDATE stage
        SET stage.OldParentCode = src.ParentCode
        FROM #MenuStage stage
        INNER JOIN dbo.MenuAdmins src
            ON src.MenuCode = stage.OldMenuCode;

        UPDATE m
        SET ParentMenuId = parent.NewMenuId
        FROM dbo.Menus m
        INNER JOIN #MenuStage child
            ON child.NewMenuId = m.MenuId
        LEFT JOIN #MenuStage parent
            ON parent.OldMenuCode = NULLIF(LTRIM(RTRIM(child.OldParentCode)), '')
        WHERE NULLIF(LTRIM(RTRIM(child.OldParentCode)), '') IS NOT NULL
          AND LTRIM(RTRIM(child.OldParentCode)) <> '0';
    END

    -------------------------------------------------------------------------
    -- Step 5: copy permissions
    -------------------------------------------------------------------------
    IF OBJECT_ID('dbo.Permissions', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Permissions)
    BEGIN
        INSERT INTO dbo.Permissions (
            PermissionCode, MenuId, ActionName, ApiName, HttpMethod,
            Visible, OrderNo, IsDeleted,
            CreatedById, CreatedAt, UpdatedById, UpdatedAt
        )
        OUTPUT
            inserted.PermissionCode,
            inserted.PermissionId
        INTO #PermissionStage (OldPermissionCode, NewPermissionId)
        SELECT
            src.MenuDetailCode,
            m.NewMenuId,
            src.ActionName,
            src.ApiName,
            NULL,
            src.Visible,
            src.OrderNo,
            CASE WHEN src.DelFlag = 0 THEN 0 ELSE 1 END,
            TRY_CONVERT(bigint, src.CreatedUserId),
            src.CreatedDateTime,
            TRY_CONVERT(bigint, src.ModifiedUserId),
            src.ModifiedDateTime
        FROM dbo.MenuAdminDetails src
        INNER JOIN #MenuStage m
            ON m.OldMenuCode = src.ParentMenuCode
        WHERE src.DelFlag = 0;

        UPDATE stage
        SET stage.OldParentCode = src.ParentMenuCode
        FROM #PermissionStage stage
        INNER JOIN dbo.MenuAdminDetails src
            ON src.MenuDetailCode = stage.OldPermissionCode;
    END

    -------------------------------------------------------------------------
    -- Step 6: copy role assignments from the legacy table if it exists
    -------------------------------------------------------------------------
    IF OBJECT_ID('dbo.RoleMenus_Legacy', 'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.RoleMenus (
            RoleId, MenuId, IsDeleted,
            CreatedById, CreatedAt, UpdatedById, UpdatedAt
        )
        SELECT DISTINCT
            COALESCE(roleById.Id, roleByCode.Id) AS RoleId,
            menuStage.NewMenuId,
            0,
            TRY_CONVERT(bigint, src.CreatedUserId),
            src.CreatedDateTime,
            TRY_CONVERT(bigint, src.ModifiedUserId),
            src.ModifiedDateTime
        FROM dbo.RoleMenus_Legacy src
        LEFT JOIN dbo.Roles roleById
            ON roleById.Id = src.RoleId
           AND roleById.IsDeleted <> 1
        LEFT JOIN dbo.Roles roleByCode
            ON roleByCode.Name = src.RoleCode
           AND roleByCode.IsDeleted <> 1
        INNER JOIN #MenuStage menuStage
            ON menuStage.OldMenuCode = src.MenuCode
        WHERE src.DelFlag = 0
          AND COALESCE(roleById.Id, roleByCode.Id) IS NOT NULL;

        INSERT INTO dbo.RolePermissions (
            RoleId, PermissionId, IsDeleted,
            CreatedById, CreatedAt, UpdatedById, UpdatedAt
        )
        SELECT DISTINCT
            COALESCE(roleById.Id, roleByCode.Id) AS RoleId,
            permStage.NewPermissionId,
            0,
            TRY_CONVERT(bigint, src.CreatedUserId),
            src.CreatedDateTime,
            TRY_CONVERT(bigint, src.ModifiedUserId),
            src.ModifiedDateTime
        FROM dbo.RoleMenus_Legacy src
        LEFT JOIN dbo.Roles roleById
            ON roleById.Id = src.RoleId
           AND roleById.IsDeleted <> 1
        LEFT JOIN dbo.Roles roleByCode
            ON roleByCode.Name = src.RoleCode
           AND roleByCode.IsDeleted <> 1
        INNER JOIN #PermissionStage permStage
            ON permStage.OldPermissionCode = src.MenuCode
        WHERE src.DelFlag = 0
          AND COALESCE(roleById.Id, roleByCode.Id) IS NOT NULL;

        INSERT INTO dbo.RoleMenus (
            RoleId, MenuId, IsDeleted,
            CreatedById, CreatedAt, UpdatedById, UpdatedAt
        )
        SELECT DISTINCT
            COALESCE(roleById.Id, roleByCode.Id) AS RoleId,
            parentMenu.NewMenuId,
            0,
            TRY_CONVERT(bigint, src.CreatedUserId),
            src.CreatedDateTime,
            TRY_CONVERT(bigint, src.ModifiedUserId),
            src.ModifiedDateTime
        FROM dbo.RoleMenus_Legacy src
        LEFT JOIN dbo.Roles roleById
            ON roleById.Id = src.RoleId
           AND roleById.IsDeleted <> 1
        LEFT JOIN dbo.Roles roleByCode
            ON roleByCode.Name = src.RoleCode
           AND roleByCode.IsDeleted <> 1
        INNER JOIN #PermissionStage permStage
            ON permStage.OldPermissionCode = src.MenuCode
        INNER JOIN #MenuStage parentMenu
            ON parentMenu.OldMenuCode = permStage.OldParentCode
        WHERE src.DelFlag = 0
          AND COALESCE(roleById.Id, roleByCode.Id) IS NOT NULL;
    END

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
