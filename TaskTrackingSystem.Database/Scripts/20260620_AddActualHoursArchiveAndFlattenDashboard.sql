IF COL_LENGTH('dbo.Tasks', 'ActualHours') IS NULL
BEGIN
    ALTER TABLE [dbo].[Tasks]
        ADD [ActualHours] [decimal](5, 2) NULL;
END
GO

IF COL_LENGTH('dbo.Tasks', 'IsArchived') IS NULL
BEGIN
    ALTER TABLE [dbo].[Tasks]
        ADD [IsArchived] [bit] NOT NULL
            CONSTRAINT [DF_Tasks_IsArchived] DEFAULT ((0));
END
GO

UPDATE [dbo].[Menus]
SET [Visible] = 0,
    [IsDeleted] = 1,
    [UpdatedAt] = SYSUTCDATETIME()
WHERE [MenuCode] = N'DASHBOARD';
GO

UPDATE [dbo].[Menus]
SET [ParentMenuId] = NULL,
    [MenuName] = N'Dashboard',
    [Visible] = 1,
    [OrderNo] = 0,
    [IsDeleted] = 0,
    [UpdatedAt] = SYSUTCDATETIME()
WHERE [MenuCode] IN (N'DASHBOARD_ADMIN', N'DASHBOARD_MANAGER', N'DASHBOARD_EMPLOYEE');
GO
