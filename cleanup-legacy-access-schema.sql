/*
Legacy access-schema cleanup script for SQL Server Management Studio.

Run this only after:
1. You have a backup of the database.
2. The new schema is already in place.
3. You have confirmed that dbo.Menus, dbo.Permissions, dbo.RoleMenus, and dbo.RolePermissions are working.

This script removes the old scaffolded tables:
- dbo.MenuAdmins
- dbo.MenuAdminDetails
- dbo.RoleMenus_Legacy
*/

SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID('dbo.RoleMenus_Legacy', 'U') IS NOT NULL
    BEGIN
        DROP TABLE dbo.RoleMenus_Legacy;
    END

    IF OBJECT_ID('dbo.MenuAdminDetails', 'U') IS NOT NULL
    BEGIN
        DROP TABLE dbo.MenuAdminDetails;
    END

    IF OBJECT_ID('dbo.MenuAdmins', 'U') IS NOT NULL
    BEGIN
        DROP TABLE dbo.MenuAdmins;
    END

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
