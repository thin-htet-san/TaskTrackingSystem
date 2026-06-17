/*
Safe reset for the sample role/menu/permission setup.

What this script fixes:
- Admin gets the full menu tree.
- Manager gets project/task/report menus with the correct permissions.
- Employee gets the smaller scoped menu tree.
- The page tree stays clickable because both parent menus and leaf pages are assigned.

It also restores a few project memberships so Task Assign / Project Assign can show people.

Run this only after your normalized menu/permission tables already exist.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @AdminRoleId bigint = (SELECT TOP (1) Id FROM dbo.Roles WHERE Name = N'Admin' AND IsDeleted = 0);
DECLARE @ManagerRoleId bigint = (SELECT TOP (1) Id FROM dbo.Roles WHERE Name = N'Manager' AND IsDeleted = 0);
DECLARE @EmployeeRoleId bigint = (SELECT TOP (1) Id FROM dbo.Roles WHERE Name = N'Employee' AND IsDeleted = 0);

IF @AdminRoleId IS NULL OR @ManagerRoleId IS NULL OR @EmployeeRoleId IS NULL
BEGIN
    RAISERROR('One or more sample roles are missing. Expected Admin, Manager, Employee.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

IF COL_LENGTH('dbo.RoleMenus', 'MenuId') IS NULL OR COL_LENGTH('dbo.RolePermissions', 'PermissionId') IS NULL
BEGIN
    RAISERROR('dbo.RoleMenus or dbo.RolePermissions does not look like the normalized schema yet.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

DECLARE @MenuDashboard bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuCode = N'DASHBOARD' OR MenuUrl = N'/dashboard'));
DECLARE @MenuProjects bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuCode = N'PROJECTS' OR MenuUrl = N'/projects'));
DECLARE @MenuProjectList bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuUrl = N'/projects/all' OR MenuName = N'Project List' OR MenuCode = N'PROJECT_LIST'));
DECLARE @MenuProjectAssign bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuUrl = N'/projects/assign' OR MenuName = N'Project Assign' OR MenuCode = N'PROJECT_ASSIGN'));

DECLARE @MenuTasks bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuCode = N'TASKS' OR MenuUrl = N'/tasks'));
DECLARE @MenuTaskList bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuUrl = N'/tasks/all' OR MenuName = N'Task List' OR MenuCode = N'TASK_LIST'));
DECLARE @MenuKanban bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuUrl = N'/board' OR MenuUrl = N'/projects/{ProjectId:long}/tasks' OR MenuName = N'Kanban Board' OR MenuCode = N'KANBAN_BOARD'));
DECLARE @MenuTaskAssign bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuUrl = N'/tasks/assign' OR MenuName = N'Task Assign' OR MenuCode = N'TASK_ASSIGN'));

DECLARE @MenuReports bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuCode = N'REPORTS' OR MenuUrl = N'/reports'));
DECLARE @MenuProjectReport bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuUrl = N'/reports/projects' OR MenuName = N'Project Progress' OR MenuName = N'Project Progress Report' OR MenuCode = N'REPORT_PROJECTS'));
DECLARE @MenuTaskReport bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuUrl = N'/reports/tasks' OR MenuName = N'Task Report' OR MenuCode = N'REPORT_TASKS'));
DECLARE @MenuOverdueReport bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuUrl = N'/reports/overdue' OR MenuName = N'Overdue Tasks' OR MenuCode = N'REPORT_OVERDUE'));
DECLARE @MenuTimesheetReport bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuUrl = N'/reports/timesheet' OR MenuName = N'Time Tracking' OR MenuName = N'Time Tracking Report' OR MenuCode = N'REPORT_TIMESHEET'));
DECLARE @MenuEmployeeReport bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuUrl = N'/reports/employees' OR MenuName = N'Employee Report' OR MenuCode = N'REPORT_EMPLOYEES'));

DECLARE @MenuUsers bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuCode = N'USERS' OR MenuUrl = N'/users'));
DECLARE @MenuRoles bigint = (SELECT TOP (1) MenuId FROM dbo.Menus WHERE IsDeleted = 0 AND (MenuCode = N'ROLES' OR MenuUrl = N'/roles'));

IF @MenuDashboard IS NULL OR @MenuProjects IS NULL OR @MenuTasks IS NULL OR @MenuReports IS NULL OR @MenuUsers IS NULL OR @MenuRoles IS NULL
BEGIN
    RAISERROR('One or more top-level menus were not found. Check the seeded Menus table.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

IF @MenuProjectList IS NULL OR @MenuProjectAssign IS NULL OR @MenuTaskList IS NULL OR @MenuKanban IS NULL OR @MenuTaskAssign IS NULL
   OR @MenuProjectReport IS NULL OR @MenuTaskReport IS NULL OR @MenuOverdueReport IS NULL OR @MenuTimesheetReport IS NULL OR @MenuEmployeeReport IS NULL
BEGIN
    RAISERROR('One or more child menus were not found. Check the seeded Menus table.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

-- Keep the list pages on their alias routes so they do not overlap with assign pages.
UPDATE dbo.Menus
SET MenuUrl = N'/projects/all'
WHERE MenuId = @MenuProjectList
  AND ISNULL(MenuUrl, N'') <> N'/projects/all';

UPDATE dbo.Menus
SET MenuUrl = N'/tasks/all'
WHERE MenuId = @MenuTaskList
  AND ISNULL(MenuUrl, N'') <> N'/tasks/all';

DECLARE @ProjectsListPermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Projects_List');
DECLARE @ProjectsCreatePermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Projects_Create');
DECLARE @ProjectsUpdatePermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Projects_Update');
DECLARE @ProjectsDeletePermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Projects_Delete');
DECLARE @TasksListPermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Tasks_List');
DECLARE @TasksCreatePermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Tasks_Create');
DECLARE @TasksUpdatePermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Tasks_Update');
DECLARE @TasksDeletePermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Tasks_Delete');
DECLARE @UsersListPermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Users_List');
DECLARE @UsersCreatePermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Users_Create');
DECLARE @UsersUpdatePermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Users_Update');
DECLARE @UsersDeletePermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Users_Delete');
DECLARE @RolesListPermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Roles_List');
DECLARE @RolesCreatePermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Roles_Create');
DECLARE @RolesUpdatePermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Roles_Update');
DECLARE @RolesDeletePermissionId bigint = (SELECT TOP (1) PermissionId FROM dbo.Permissions WHERE IsDeleted = 0 AND PermissionCode = N'Roles_Delete');

IF @ProjectsListPermissionId IS NULL OR @TasksListPermissionId IS NULL OR @TasksUpdatePermissionId IS NULL
   OR @ProjectsCreatePermissionId IS NULL OR @ProjectsUpdatePermissionId IS NULL OR @ProjectsDeletePermissionId IS NULL
   OR @TasksCreatePermissionId IS NULL OR @TasksDeletePermissionId IS NULL
   OR @UsersListPermissionId IS NULL OR @UsersCreatePermissionId IS NULL OR @UsersUpdatePermissionId IS NULL OR @UsersDeletePermissionId IS NULL
   OR @RolesListPermissionId IS NULL OR @RolesCreatePermissionId IS NULL OR @RolesUpdatePermissionId IS NULL OR @RolesDeletePermissionId IS NULL
BEGIN
    RAISERROR('One or more permissions were not found. Check the seeded Permissions table.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

DELETE FROM dbo.RoleMenus WHERE RoleId IN (@AdminRoleId, @ManagerRoleId, @EmployeeRoleId);
DELETE FROM dbo.RolePermissions WHERE RoleId IN (@AdminRoleId, @ManagerRoleId, @EmployeeRoleId);

INSERT INTO dbo.RoleMenus (RoleId, MenuId, IsDeleted, CreatedAt)
SELECT @AdminRoleId, MenuId, 0, SYSUTCDATETIME()
FROM (VALUES
    (@MenuDashboard),
    (@MenuProjects),
    (@MenuProjectList),
    (@MenuProjectAssign),
    (@MenuTasks),
    (@MenuTaskList),
    (@MenuKanban),
    (@MenuTaskAssign),
    (@MenuReports),
    (@MenuProjectReport),
    (@MenuTaskReport),
    (@MenuOverdueReport),
    (@MenuTimesheetReport),
    (@MenuEmployeeReport),
    (@MenuUsers),
    (@MenuRoles)
) AS V(MenuId);

INSERT INTO dbo.RoleMenus (RoleId, MenuId, IsDeleted, CreatedAt)
SELECT @ManagerRoleId, MenuId, 0, SYSUTCDATETIME()
FROM (VALUES
    (@MenuDashboard),
    (@MenuProjects),
    (@MenuProjectList),
    (@MenuProjectAssign),
    (@MenuTasks),
    (@MenuTaskList),
    (@MenuKanban),
    (@MenuTaskAssign),
    (@MenuReports),
    (@MenuProjectReport),
    (@MenuTaskReport),
    (@MenuOverdueReport),
    (@MenuTimesheetReport)
) AS V(MenuId);

INSERT INTO dbo.RoleMenus (RoleId, MenuId, IsDeleted, CreatedAt)
SELECT @EmployeeRoleId, MenuId, 0, SYSUTCDATETIME()
FROM (VALUES
    (@MenuDashboard),
    (@MenuProjects),
    (@MenuProjectList),
    (@MenuTasks),
    (@MenuTaskList),
    (@MenuKanban),
    (@MenuReports),
    (@MenuTaskReport),
    (@MenuTimesheetReport)
) AS V(MenuId);

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, IsDeleted, CreatedAt)
SELECT @AdminRoleId, PermissionId, 0, SYSUTCDATETIME()
FROM (VALUES
    (@ProjectsListPermissionId),
    (@ProjectsCreatePermissionId),
    (@ProjectsUpdatePermissionId),
    (@ProjectsDeletePermissionId),
    (@TasksListPermissionId),
    (@TasksCreatePermissionId),
    (@TasksUpdatePermissionId),
    (@TasksDeletePermissionId),
    (@UsersListPermissionId),
    (@UsersCreatePermissionId),
    (@UsersUpdatePermissionId),
    (@UsersDeletePermissionId),
    (@RolesListPermissionId),
    (@RolesCreatePermissionId),
    (@RolesUpdatePermissionId),
    (@RolesDeletePermissionId)
) AS V(PermissionId);

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, IsDeleted, CreatedAt)
SELECT @ManagerRoleId, PermissionId, 0, SYSUTCDATETIME()
FROM (VALUES
    (@ProjectsListPermissionId),
    (@ProjectsUpdatePermissionId),
    (@TasksListPermissionId),
    (@TasksCreatePermissionId),
    (@TasksUpdatePermissionId)
) AS V(PermissionId);

INSERT INTO dbo.RolePermissions (RoleId, PermissionId, IsDeleted, CreatedAt)
SELECT @EmployeeRoleId, PermissionId, 0, SYSUTCDATETIME()
FROM (VALUES
    (@ProjectsListPermissionId),
    (@TasksListPermissionId),
    (@TasksUpdatePermissionId)
) AS V(PermissionId);

DECLARE @ProjectIds TABLE (Slot int IDENTITY(1,1), ProjectId bigint);
INSERT INTO @ProjectIds (ProjectId)
SELECT TOP (5) Id
FROM dbo.Projects
WHERE IsDeleted = 0
ORDER BY Id;

IF (SELECT COUNT(*) FROM @ProjectIds) = 0
BEGIN
    RAISERROR('No projects were found to attach sample members to.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

DELETE FROM dbo.ProjectMembers
WHERE UserId IN (
    SELECT Id
    FROM dbo.Users
    WHERE IsDeleted = 0
      AND (Username LIKE N'mgr%' OR Username LIKE N'emp%')
);

INSERT INTO dbo.ProjectMembers (ProjectId, UserId, CreatedAt, CreatedBy)
SELECT p.ProjectId, u.Id, SYSUTCDATETIME(), NULL
FROM @ProjectIds p
JOIN dbo.Users u
    ON u.IsDeleted = 0
   AND (
        (p.Slot = 1 AND u.Username IN (N'mgr01', N'mgr06', N'emp01', N'emp02', N'emp03', N'emp04', N'emp05', N'emp06'))
     OR (p.Slot = 2 AND u.Username IN (N'mgr02', N'mgr07', N'emp07', N'emp08', N'emp09', N'emp10', N'emp11', N'emp12'))
     OR (p.Slot = 3 AND u.Username IN (N'mgr03', N'mgr08', N'emp13', N'emp14', N'emp15', N'emp16', N'emp17', N'emp18'))
     OR (p.Slot = 4 AND u.Username IN (N'mgr04', N'mgr09', N'emp19', N'emp20', N'emp21', N'emp22', N'emp23', N'emp24'))
     OR (p.Slot = 5 AND u.Username IN (N'mgr05', N'mgr10', N'emp25', N'emp26', N'emp27', N'emp28', N'emp29', N'emp30'))
   );

COMMIT TRANSACTION;
