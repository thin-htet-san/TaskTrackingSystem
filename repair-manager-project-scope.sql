/*
Repair script for the current sample data set.

Use this when:
1. The menu is visible, but Manager/Employee pages do not return data.
2. Task assignment is empty because the manager is not attached to any project rows.
3. "Can see page but cannot do anything" happens because the role lost update/list permissions.

What this script does:
- Rebuilds the Manager and Employee role menu + permission mappings.
- Adds sample project memberships so TaskAssign and ProjectAssign have real project members.

Assumptions:
- You already have dbo.Roles, dbo.Users, dbo.Projects, dbo.Menus, dbo.Permissions,
  dbo.RoleMenus, dbo.RolePermissions, and dbo.ProjectMembers.
- Your sample usernames follow the patterns mgr01..mgr10 and emp01..emp30.
- Your projects already exist in dbo.Projects.

This script is safe to run multiple times.
*/

SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @AdminRoleId bigint = (SELECT TOP (1) Id FROM dbo.Roles WHERE Name = N'Admin' AND IsDeleted = 0);
    DECLARE @ManagerRoleId bigint = (SELECT TOP (1) Id FROM dbo.Roles WHERE Name = N'Manager' AND IsDeleted = 0);
    DECLARE @EmployeeRoleId bigint = (SELECT TOP (1) Id FROM dbo.Roles WHERE Name = N'Employee' AND IsDeleted = 0);

    IF @ManagerRoleId IS NULL
        THROW 50001, 'Manager role not found.', 1;

    IF @EmployeeRoleId IS NULL
        THROW 50002, 'Employee role not found.', 1;

    -------------------------------------------------------------------------
    -- Permissions and menus used by the role/access UI
    -------------------------------------------------------------------------
    DECLARE @MenuProjectsId bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE MenuCode = N'PROJECTS');
    DECLARE @MenuTasksId bigint    = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE MenuCode = N'TASKS');
    DECLARE @MenuReportsId bigint  = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE MenuCode = N'REPORTS');
    DECLARE @MenuUsersId bigint    = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE MenuCode = N'USERS');
    DECLARE @MenuRolesId bigint    = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE MenuCode = N'ROLES');

    ;WITH PermissionSeed AS
    (
        SELECT N'Projects_List'   AS PermissionCode, @MenuProjectsId AS MenuId, N'List'   AS ActionName, N'api/Project' AS ApiName, N'GET'    AS HttpMethod, 1 AS Visible, 10 AS OrderNo
        UNION ALL SELECT N'Projects_Create', @MenuProjectsId, N'Create', N'api/Project', N'POST', 1, 20
        UNION ALL SELECT N'Projects_Update', @MenuProjectsId, N'Update', N'api/Project', N'PUT', 1, 30
        UNION ALL SELECT N'Projects_Delete', @MenuProjectsId, N'Delete', N'api/Project', N'DELETE', 1, 40
        UNION ALL SELECT N'Tasks_List',      @MenuTasksId,    N'List',   N'api/Task',    N'GET',    1, 10
        UNION ALL SELECT N'Tasks_Create',    @MenuTasksId,    N'Create', N'api/Task',    N'POST',   1, 20
        UNION ALL SELECT N'Tasks_Update',    @MenuTasksId,    N'Update', N'api/Task',    N'PUT',    1, 30
        UNION ALL SELECT N'Tasks_Delete',    @MenuTasksId,    N'Delete', N'api/Task',    N'DELETE', 1, 40
        UNION ALL SELECT N'Users_List',      @MenuUsersId,    N'List',   N'api/User',    N'GET',    1, 10
        UNION ALL SELECT N'Users_Create',    @MenuUsersId,    N'Create', N'api/User',    N'POST',   1, 20
        UNION ALL SELECT N'Users_Update',    @MenuUsersId,    N'Update', N'api/User',    N'PUT',    1, 30
        UNION ALL SELECT N'Users_Delete',    @MenuUsersId,    N'Delete', N'api/User',    N'DELETE', 1, 40
        UNION ALL SELECT N'Roles_List',      @MenuRolesId,    N'List',   N'api/Role',    N'GET',    1, 10
        UNION ALL SELECT N'Roles_Create',    @MenuRolesId,    N'Create', N'api/Role',    N'POST',   1, 20
        UNION ALL SELECT N'Roles_Update',    @MenuRolesId,    N'Update', N'api/Role',    N'PUT',    1, 30
        UNION ALL SELECT N'Roles_Delete',    @MenuRolesId,    N'Delete', N'api/Role',    N'DELETE', 1, 40
    )
    INSERT INTO dbo.Permissions
    (
        PermissionCode,
        MenuId,
        ActionName,
        ApiName,
        HttpMethod,
        Visible,
        OrderNo,
        IsDeleted,
        CreatedAt
    )
    SELECT
        p.PermissionCode,
        p.MenuId,
        p.ActionName,
        p.ApiName,
        p.HttpMethod,
        p.Visible,
        p.OrderNo,
        0,
        SYSUTCDATETIME()
    FROM PermissionSeed p
    WHERE p.MenuId IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.Permissions existing
          WHERE existing.PermissionCode = p.PermissionCode
      );

    -------------------------------------------------------------------------
    -- Rebuild role-menu mappings for Manager and Employee
    -------------------------------------------------------------------------
    IF @ManagerRoleId IS NOT NULL
    BEGIN
        DELETE FROM dbo.RoleMenus WHERE RoleId = @ManagerRoleId;
    END

    IF @EmployeeRoleId IS NOT NULL
    BEGIN
        DELETE FROM dbo.RoleMenus WHERE RoleId = @EmployeeRoleId;
    END

    ;WITH RoleMenuSeed AS
    (
        SELECT @ManagerRoleId AS RoleId, N'DASHBOARD' AS MenuCode
        UNION ALL SELECT @ManagerRoleId, N'PROJECTS'
        UNION ALL SELECT @ManagerRoleId, N'TASKS'
        UNION ALL SELECT @ManagerRoleId, N'REPORTS'
        UNION ALL SELECT @ManagerRoleId, N'USERS'

        UNION ALL SELECT @EmployeeRoleId, N'DASHBOARD'
        UNION ALL SELECT @EmployeeRoleId, N'PROJECTS'
        UNION ALL SELECT @EmployeeRoleId, N'TASKS'
        UNION ALL SELECT @EmployeeRoleId, N'REPORTS'
    )
    INSERT INTO dbo.RoleMenus
    (
        RoleId,
        MenuId,
        IsDeleted,
        CreatedAt
    )
    SELECT DISTINCT
        s.RoleId,
        m.MenuId,
        0,
        SYSUTCDATETIME()
    FROM RoleMenuSeed s
    INNER JOIN dbo.Menus m
        ON m.MenuCode = s.MenuCode
    WHERE s.RoleId IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.RoleMenus rm
          WHERE rm.RoleId = s.RoleId
            AND rm.MenuId = m.MenuId
      );

    -------------------------------------------------------------------------
    -- Rebuild role-permission mappings
    -------------------------------------------------------------------------
    IF @ManagerRoleId IS NOT NULL
    BEGIN
        DELETE FROM dbo.RolePermissions WHERE RoleId = @ManagerRoleId;
    END

    IF @EmployeeRoleId IS NOT NULL
    BEGIN
        DELETE FROM dbo.RolePermissions WHERE RoleId = @EmployeeRoleId;
    END

    ;WITH RolePermissionSeed AS
    (
        SELECT @ManagerRoleId AS RoleId, N'Projects_List' AS PermissionCode
        UNION ALL SELECT @ManagerRoleId, N'Projects_Update'
        UNION ALL SELECT @ManagerRoleId, N'Tasks_List'
        UNION ALL SELECT @ManagerRoleId, N'Tasks_Create'
        UNION ALL SELECT @ManagerRoleId, N'Tasks_Update'
        UNION ALL SELECT @ManagerRoleId, N'Users_List'

        UNION ALL SELECT @EmployeeRoleId, N'Projects_List'
        UNION ALL SELECT @EmployeeRoleId, N'Tasks_List'
        UNION ALL SELECT @EmployeeRoleId, N'Tasks_Update'
    )
    INSERT INTO dbo.RolePermissions
    (
        RoleId,
        PermissionId,
        IsDeleted,
        CreatedAt
    )
    SELECT DISTINCT
        s.RoleId,
        p.PermissionId,
        0,
        SYSUTCDATETIME()
    FROM RolePermissionSeed s
    INNER JOIN dbo.Permissions p
        ON p.PermissionCode = s.PermissionCode
    WHERE s.RoleId IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.RolePermissions rp
          WHERE rp.RoleId = s.RoleId
            AND rp.PermissionId = p.PermissionId
      );

    -------------------------------------------------------------------------
    -- Sample project membership seed
    -- This spreads managers and employees across the first 5 projects found.
    -------------------------------------------------------------------------
    ;WITH ProjectOrder AS
    (
        SELECT
            p.Id,
            ROW_NUMBER() OVER (ORDER BY p.Id) AS ProjectSlot
        FROM dbo.Projects p
        WHERE p.IsDeleted = 0
    ),
    MembershipSeed AS
    (
        SELECT N'mgr01' AS Username, 1 AS ProjectSlot UNION ALL
        SELECT N'mgr06', 1 UNION ALL
        SELECT N'emp01', 1 UNION ALL
        SELECT N'emp02', 1 UNION ALL
        SELECT N'emp03', 1 UNION ALL
        SELECT N'emp04', 1 UNION ALL
        SELECT N'emp05', 1 UNION ALL
        SELECT N'emp06', 1 UNION ALL

        SELECT N'mgr02', 2 UNION ALL
        SELECT N'mgr07', 2 UNION ALL
        SELECT N'emp07', 2 UNION ALL
        SELECT N'emp08', 2 UNION ALL
        SELECT N'emp09', 2 UNION ALL
        SELECT N'emp10', 2 UNION ALL
        SELECT N'emp11', 2 UNION ALL
        SELECT N'emp12', 2 UNION ALL

        SELECT N'mgr03', 3 UNION ALL
        SELECT N'mgr08', 3 UNION ALL
        SELECT N'emp13', 3 UNION ALL
        SELECT N'emp14', 3 UNION ALL
        SELECT N'emp15', 3 UNION ALL
        SELECT N'emp16', 3 UNION ALL
        SELECT N'emp17', 3 UNION ALL
        SELECT N'emp18', 3 UNION ALL

        SELECT N'mgr04', 4 UNION ALL
        SELECT N'mgr09', 4 UNION ALL
        SELECT N'emp19', 4 UNION ALL
        SELECT N'emp20', 4 UNION ALL
        SELECT N'emp21', 4 UNION ALL
        SELECT N'emp22', 4 UNION ALL
        SELECT N'emp23', 4 UNION ALL
        SELECT N'emp24', 4 UNION ALL

        SELECT N'mgr05', 5 UNION ALL
        SELECT N'mgr10', 5 UNION ALL
        SELECT N'emp25', 5 UNION ALL
        SELECT N'emp26', 5 UNION ALL
        SELECT N'emp27', 5 UNION ALL
        SELECT N'emp28', 5 UNION ALL
        SELECT N'emp29', 5 UNION ALL
        SELECT N'emp30', 5
    )
    INSERT INTO dbo.ProjectMembers
    (
        ProjectId,
        UserId,
        CreatedAt,
        CreatedBy
    )
    SELECT
        po.Id,
        u.Id,
        SYSUTCDATETIME(),
        NULL
    FROM MembershipSeed s
    INNER JOIN dbo.Users u
        ON u.Username = s.Username
       AND u.IsDeleted = 0
    INNER JOIN ProjectOrder po
        ON po.ProjectSlot = s.ProjectSlot
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.ProjectMembers pm
        WHERE pm.ProjectId = po.Id
          AND pm.UserId = u.Id
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
