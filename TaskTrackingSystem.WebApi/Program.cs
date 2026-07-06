using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using TaskTrackingSystem.Database.AppDbContextModels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();

// CORS policy for WebApp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp", policy =>
    {
        policy.WithOrigins("http://localhost:5247", "https://localhost:7176")
              .AllowCredentials()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Infrastructure.AuditLogService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.User.UserService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Auth.AuthService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Role.RoleService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Project.ProjectService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Task.TaskService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Issue.IssueService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Dashboard.DashboardService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Report.ReportService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Menu.MenuService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.UserDevice.UserDeviceService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Notification.FirebaseNotificationService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Notification.NotificationRealtimeService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Notification.NotificationService>();
builder.Services.AddHostedService<TaskTrackingSystem.WebApi.Features.Notification.NotificationCleanupHostedService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Infrastructure.PermissionAuthorizationService>();
builder.Services.AddScoped<IPasswordHasher<TaskTrackingSystem.Database.AppDbContextModels.User>, PasswordHasher<TaskTrackingSystem.Database.AppDbContextModels.User>>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT token from the login response. You can include or omit the 'Bearer ' prefix."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var key = jwtSettings["Key"] ?? throw new InvalidOperationException("JwtSettings:Key must be configured.");
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JwtSettings:Issuer must be configured."),
        ValidAudience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JwtSettings:Audience must be configured."),
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
    };
});

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

var app = builder.Build();

await EnsureSeedDataAsync(app);


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.EnablePersistAuthorization();
    });
}

app.UseCors("AllowWebApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.MapHub<TaskTrackingSystem.WebApi.Features.Notification.NotificationHub>("/hubs/notifications");

app.Run();

static async System.Threading.Tasks.Task EnsureSeedDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.EnsureCreatedAsync();
    // In Supabase/Hugging Face, EnsureCreatedAsync() might skip table creation 
    // because other schemas (auth, storage, etc.) contain tables.
    // We check if the 'Menus' table exists in the public schema, and force creation of tables if it doesn't.
    var databaseCreator = db.Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
    if (databaseCreator != null)
    {
        var connection = db.Database.GetDbConnection();
        var connectionOpened = false;
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync();
            connectionOpened = true;
        }
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'Menus');";
            var tableExists = (bool)(await command.ExecuteScalarAsync() ?? false);
            if (!tableExists)
            {
                await databaseCreator.CreateTablesAsync();
            }
        }
        finally
        {
            if (connectionOpened)
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }

    await EnsureReportUpgradeSchemaAsync(db);

    // Seed Roles, Users, Menus and Permissions if database is completely empty of roles (e.g. on Supabase)
    if (!await db.Roles.AnyAsync())
    {
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        // 1. Seed Roles
        var newAdminRole = new Role { Name = "Admin", Description = "Full system access", CreatedAt = DateTime.UtcNow };
        var newManagerRole = new Role { Name = "Manager", Description = "Project-scoped manager", CreatedAt = DateTime.UtcNow };
        var newEmployeeRole = new Role { Name = "Employee", Description = "Task-level team member", CreatedAt = DateTime.UtcNow };
        var newSupervisorRole = new Role { Name = "Supervisor", CreatedAt = DateTime.UtcNow };
        db.Roles.AddRange(newAdminRole, newManagerRole, newEmployeeRole, newSupervisorRole);
        await db.SaveChangesAsync();

        // 2. Seed Users
        var adminUser = new User 
        { 
            Username = "admin", 
            FirstName = "System", 
            LastName = "Admin", 
            Email = "admin@tts.local", 
            RoleId = newAdminRole.Id, 
            IsActive = true, 
            CreatedAt = DateTime.UtcNow 
        };
        adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "P@ssw0rd123!");

        var managerUser = new User 
        { 
            Username = "mgr01", 
            FirstName = "Kyaw Kyaw", 
            LastName = "Aung", 
            Email = "mgr01@tts.local", 
            RoleId = newManagerRole.Id, 
            IsActive = true, 
            CreatedAt = DateTime.UtcNow 
        };
        managerUser.PasswordHash = passwordHasher.HashPassword(managerUser, "P@ssw0rd123!");

        var employeeUser = new User 
        { 
            Username = "emp01", 
            FirstName = "Hla Hla", 
            LastName = "Khin", 
            Email = "emp01@tts.local", 
            RoleId = newEmployeeRole.Id, 
            IsActive = true, 
            CreatedAt = DateTime.UtcNow 
        };
        employeeUser.PasswordHash = passwordHasher.HashPassword(employeeUser, "P@ssw0rd123!");

        db.Users.AddRange(adminUser, managerUser, employeeUser);
        await db.SaveChangesAsync();

        // 3. Seed Menus
        var menuProjects = new Menu { MenuCode = "PROJECTS", MenuName = "Projects", Icon = "folder", Visible = true, OrderNo = 1, CreatedAt = DateTime.UtcNow };
        var menuProjectsAdd = new Menu { MenuCode = "PROJECTS_ADD", ParentMenu = menuProjects, MenuName = "Add Project", MenuUrl = "/projects/add", Icon = "plus", Visible = true, OrderNo = 2, CreatedAt = DateTime.UtcNow };
        var menuProjectsList = new Menu { MenuCode = "PROJECTS_LIST", ParentMenu = menuProjects, MenuName = "Project List", MenuUrl = "/projects/all", Icon = "list", Visible = true, OrderNo = 3, CreatedAt = DateTime.UtcNow };
        var menuProjectsAssign = new Menu { MenuCode = "PROJECTS_ASSIGN", ParentMenu = menuProjects, MenuName = "Project Assign", MenuUrl = "/projects/assign", Icon = "users-round", Visible = true, OrderNo = 4, CreatedAt = DateTime.UtcNow };

        var menuTasks = new Menu { MenuCode = "TASKS", MenuName = "Tasks", Icon = "check-square", Visible = true, OrderNo = 5, CreatedAt = DateTime.UtcNow };
        var menuTasksList = new Menu { MenuCode = "TASKS_LIST", ParentMenu = menuTasks, MenuName = "Task List", MenuUrl = "/tasks/all", Icon = "list-todo", Visible = true, OrderNo = 6, CreatedAt = DateTime.UtcNow };
        var menuTasksBoard = new Menu { MenuCode = "TASKS_BOARD", ParentMenu = menuTasks, MenuName = "Kanban Board", MenuUrl = "/board", Icon = "columns-3", Visible = true, OrderNo = 7, CreatedAt = DateTime.UtcNow };
        var menuTasksAssign = new Menu { MenuCode = "TASKS_ASSIGN", ParentMenu = menuTasks, MenuName = "Task Assign", MenuUrl = "/tasks/assign", Icon = "user-check", Visible = true, OrderNo = 8, CreatedAt = DateTime.UtcNow };

        var menuReports = new Menu { MenuCode = "REPORTS", MenuName = "Reports", Icon = "chart-column", Visible = true, OrderNo = 9, CreatedAt = DateTime.UtcNow };
        var menuReportsTasks = new Menu { MenuCode = "REPORTS_TASKS", ParentMenu = menuReports, MenuName = "Task Report", MenuUrl = "/reports/tasks", Icon = "file-text", Visible = true, OrderNo = 10, CreatedAt = DateTime.UtcNow };
        var menuReportsTimesheet = new Menu { MenuCode = "REPORTS_TIMESHEET", ParentMenu = menuReports, MenuName = "Time Tracking", MenuUrl = "/reports/timesheet", Icon = "clock-3", Visible = true, OrderNo = 11, CreatedAt = DateTime.UtcNow };
        var menuReportsOverdue = new Menu { MenuCode = "REPORTS_OVERDUE", ParentMenu = menuReports, MenuName = "Overdue Tasks", MenuUrl = "/reports/overdue", Icon = "alarm-clock", Visible = true, OrderNo = 12, CreatedAt = DateTime.UtcNow };
        var menuReportsEmployees = new Menu { MenuCode = "REPORTS_EMPLOYEES", ParentMenu = menuReports, MenuName = "Employee Report", MenuUrl = "/reports/employees", Icon = "users", Visible = true, OrderNo = 13, CreatedAt = DateTime.UtcNow };
        var menuReportsProjects = new Menu { MenuCode = "REPORTS_PROJECTS", ParentMenu = menuReports, MenuName = "Project Progress", MenuUrl = "/reports/projects", Icon = "subtitles", Visible = true, OrderNo = 14, CreatedAt = DateTime.UtcNow };

        var menuUsers = new Menu { MenuCode = "USERS", MenuName = "Users", MenuUrl = "/users", Icon = "users-round", Visible = true, OrderNo = 15, CreatedAt = DateTime.UtcNow };
        var menuRoles = new Menu { MenuCode = "ROLES", MenuName = "Roles", MenuUrl = "/roles", Icon = "shield-user", Visible = true, OrderNo = 16, CreatedAt = DateTime.UtcNow };
        var menuDashboardWidgets = new Menu { MenuCode = "DASHBOARD_WIDGETS", MenuName = "Role Layouts", MenuUrl = "/widget-setup", Icon = "layout-grid", Visible = true, OrderNo = 26, CreatedAt = DateTime.UtcNow };

        var menuIssuesAdd = new Menu { MenuCode = "ISSUES_ADD", ParentMenu = menuTasks, MenuName = "Add Issue", MenuUrl = "/issues/add", Icon = "circle-plus", Visible = true, OrderNo = 26, CreatedAt = DateTime.UtcNow };
        var menuIssuesList = new Menu { MenuCode = "ISSUES_LIST", ParentMenu = menuTasks, MenuName = "Issue List", MenuUrl = "/issues/all", Icon = "list-todo", Visible = true, OrderNo = 27, CreatedAt = DateTime.UtcNow };
        var menuAuditLogs = new Menu { MenuCode = "AUDIT_LOGS", MenuName = "Audit Logs", MenuUrl = "/audit-logs", Icon = "file-text", Visible = true, OrderNo = 28, CreatedAt = DateTime.UtcNow };

        db.Menus.AddRange(
            menuProjects, menuProjectsAdd, menuProjectsList, menuProjectsAssign,
            menuTasks, menuTasksList, menuTasksBoard, menuTasksAssign,
            menuReports, menuReportsTasks, menuReportsTimesheet, menuReportsOverdue, menuReportsEmployees, menuReportsProjects,
            menuUsers, menuRoles, menuDashboardWidgets,
            menuIssuesAdd, menuIssuesList, menuAuditLogs
        );
        await db.SaveChangesAsync();

        // 4. Seed Permissions
        var permissions = new List<Permission>
        {
            new Permission { PermissionCode = "Projects_List", MenuId = menuProjectsAdd.MenuId, ActionName = "List", ApiName = "api/Project", HttpMethod = "GET", Visible = true, OrderNo = 1, CreatedAt = DateTime.UtcNow },
            new Permission { PermissionCode = "Projects_Create", MenuId = menuProjectsAdd.MenuId, ActionName = "Create", ApiName = "api/Project", HttpMethod = "POST", Visible = true, OrderNo = 2, CreatedAt = DateTime.UtcNow },
            new Permission { PermissionCode = "Projects_Update", MenuId = menuProjectsAdd.MenuId, ActionName = "Update", ApiName = "api/Project", HttpMethod = "PUT", Visible = true, OrderNo = 3, CreatedAt = DateTime.UtcNow },
            new Permission { PermissionCode = "Projects_Delete", MenuId = menuProjectsAdd.MenuId, ActionName = "Delete", ApiName = "api/Project", HttpMethod = "DELETE", Visible = true, OrderNo = 4, CreatedAt = DateTime.UtcNow },

            new Permission { PermissionCode = "Tasks_List", MenuId = menuTasks.MenuId, ActionName = "List", ApiName = "api/Task", HttpMethod = "GET", Visible = true, OrderNo = 5, CreatedAt = DateTime.UtcNow },
            new Permission { PermissionCode = "Tasks_Create", MenuId = menuTasks.MenuId, ActionName = "Create", ApiName = "api/Task", HttpMethod = "POST", Visible = true, OrderNo = 6, CreatedAt = DateTime.UtcNow },
            new Permission { PermissionCode = "Tasks_Update", MenuId = menuTasks.MenuId, ActionName = "Update", ApiName = "api/Task", HttpMethod = "PUT", Visible = true, OrderNo = 7, CreatedAt = DateTime.UtcNow },
            new Permission { PermissionCode = "Tasks_Delete", MenuId = menuTasks.MenuId, ActionName = "Delete", ApiName = "api/Task", HttpMethod = "DELETE", Visible = true, OrderNo = 8, CreatedAt = DateTime.UtcNow },

            new Permission { PermissionCode = "Users_List", MenuId = menuUsers.MenuId, ActionName = "List", ApiName = "api/User", HttpMethod = "GET", Visible = true, OrderNo = 9, CreatedAt = DateTime.UtcNow },
            new Permission { PermissionCode = "Users_Create", MenuId = menuUsers.MenuId, ActionName = "Create", ApiName = "api/User", HttpMethod = "POST", Visible = true, OrderNo = 10, CreatedAt = DateTime.UtcNow },
            new Permission { PermissionCode = "Users_Update", MenuId = menuUsers.MenuId, ActionName = "Update", ApiName = "api/User", HttpMethod = "PUT", Visible = true, OrderNo = 11, CreatedAt = DateTime.UtcNow },
            new Permission { PermissionCode = "Users_Delete", MenuId = menuUsers.MenuId, ActionName = "Delete", ApiName = "api/User", HttpMethod = "DELETE", Visible = true, OrderNo = 12, CreatedAt = DateTime.UtcNow },

            new Permission { PermissionCode = "Roles_List", MenuId = menuRoles.MenuId, ActionName = "List", ApiName = "api/Role", HttpMethod = "GET", Visible = true, OrderNo = 13, CreatedAt = DateTime.UtcNow },
            new Permission { PermissionCode = "Roles_Create", MenuId = menuRoles.MenuId, ActionName = "Create", ApiName = "api/Role", HttpMethod = "POST", Visible = true, OrderNo = 14, CreatedAt = DateTime.UtcNow },
            new Permission { PermissionCode = "Roles_Update", MenuId = menuRoles.MenuId, ActionName = "Update", ApiName = "api/Role", HttpMethod = "PUT", Visible = true, OrderNo = 15, CreatedAt = DateTime.UtcNow },
            new Permission { PermissionCode = "Roles_Delete", MenuId = menuRoles.MenuId, ActionName = "Delete", ApiName = "api/Role", HttpMethod = "DELETE", Visible = true, OrderNo = 16, CreatedAt = DateTime.UtcNow },

            new Permission { PermissionCode = "Issues_List", MenuId = menuIssuesAdd.MenuId, ActionName = "List", ApiName = "api/Issue", HttpMethod = "GET", Visible = true, OrderNo = 1, CreatedAt = DateTime.UtcNow },
            new Permission { PermissionCode = "Issues_Create", MenuId = menuIssuesAdd.MenuId, ActionName = "Create", ApiName = "api/Issue", HttpMethod = "POST", Visible = true, OrderNo = 2, CreatedAt = DateTime.UtcNow },
            new Permission { PermissionCode = "Issues_Update", MenuId = menuIssuesAdd.MenuId, ActionName = "Update", ApiName = "api/Issue", HttpMethod = "PUT", Visible = true, OrderNo = 3, CreatedAt = DateTime.UtcNow },
            new Permission { PermissionCode = "Issues_Delete", MenuId = menuIssuesAdd.MenuId, ActionName = "Delete", ApiName = "api/Issue", HttpMethod = "DELETE", Visible = true, OrderNo = 4, CreatedAt = DateTime.UtcNow },

            new Permission { PermissionCode = "AuditLogs_List", MenuId = menuAuditLogs.MenuId, ActionName = "List", ApiName = "api/AuditLog", HttpMethod = "GET", Visible = true, OrderNo = 28, CreatedAt = DateTime.UtcNow }
        };
        db.Permissions.AddRange(permissions);
        await db.SaveChangesAsync();

        // 5. Seed Role Menus and Role Permissions
        // Admin gets access to all menus
        var allMenus = await db.Menus.ToListAsync();
        foreach (var menu in allMenus)
        {
            db.RoleMenus.Add(new RoleMenu { RoleId = newAdminRole.Id, MenuId = menu.MenuId, CreatedAt = DateTime.UtcNow });
        }

        // Admin gets access to all permissions
        var allPermissions = await db.Permissions.ToListAsync();
        foreach (var permission in allPermissions)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = newAdminRole.Id, PermissionId = permission.PermissionId, CreatedAt = DateTime.UtcNow });
        }

        // Manager gets access to some menus (e.g., Projects, Tasks, Reports, Issues)
        var managerMenus = allMenus.Where(m => m.MenuCode.StartsWith("PROJECTS") || m.MenuCode.StartsWith("TASKS") || m.MenuCode.StartsWith("REPORTS") || m.MenuCode.StartsWith("ISSUES")).ToList();
        foreach (var menu in managerMenus)
        {
            db.RoleMenus.Add(new RoleMenu { RoleId = newManagerRole.Id, MenuId = menu.MenuId, CreatedAt = DateTime.UtcNow });
        }
        
        // Employee gets access to some menus
        var employeeMenuCodes = new[] { "PROJECTS_LIST", "TASKS_LIST", "TASKS_BOARD", "REPORTS_TASKS", "REPORTS_TIMESHEET", "REPORTS_OVERDUE", "ISSUES_ADD", "ISSUES_LIST" };
        var employeeMenus = allMenus.Where(m => employeeMenuCodes.Contains(m.MenuCode) || m.MenuCode == "PROJECTS" || m.MenuCode == "TASKS" || m.MenuCode == "REPORTS").ToList();
        foreach (var menu in employeeMenus)
        {
            db.RoleMenus.Add(new RoleMenu { RoleId = newEmployeeRole.Id, MenuId = menu.MenuId, CreatedAt = DateTime.UtcNow });
        }

        // Permissions for Manager & Employee
        var managerPermissions = allPermissions.Where(p => p.PermissionCode.StartsWith("Projects_") || p.PermissionCode.StartsWith("Tasks_") || p.PermissionCode.StartsWith("Issues_")).ToList();
        foreach (var permission in managerPermissions)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = newManagerRole.Id, PermissionId = permission.PermissionId, CreatedAt = DateTime.UtcNow });
        }

        var employeePermissions = allPermissions.Where(p => p.PermissionCode == "Tasks_List" || p.PermissionCode == "Tasks_Create" || p.PermissionCode == "Tasks_Update" || p.PermissionCode == "Issues_List" || p.PermissionCode == "Issues_Create").ToList();
        foreach (var permission in employeePermissions)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = newEmployeeRole.Id, PermissionId = permission.PermissionId, CreatedAt = DateTime.UtcNow });
        }

        await db.SaveChangesAsync();
    }

    var dashboardMenu = await db.Menus.FirstOrDefaultAsync(m => m.MenuCode == "DASHBOARD");
    if (dashboardMenu == null)
    {
        dashboardMenu = new Menu
        {
            MenuCode = "DASHBOARD",
            MenuName = "Dashboard",
            MenuUrl = null,
            Icon = "layout-grid",
            Visible = false,
            OrderNo = 0,
            IsDeleted = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Menus.Add(dashboardMenu);
        await db.SaveChangesAsync();
    }
    else
    {
        dashboardMenu.MenuName = "Dashboard";
        dashboardMenu.MenuUrl = null;
        dashboardMenu.Icon = "layout-grid";
        dashboardMenu.Visible = false;
        dashboardMenu.OrderNo = 0;
        dashboardMenu.IsDeleted = true;
        dashboardMenu.UpdatedAt = DateTime.UtcNow;
    }

    var adminDashboardMenu = await db.Menus.FirstOrDefaultAsync(m => m.MenuCode == "DASHBOARD_ADMIN");
    if (adminDashboardMenu == null)
    {
        adminDashboardMenu = new Menu
        {
            MenuCode = "DASHBOARD_ADMIN",
            ParentMenuId = null,
            MenuName = "Dashboard",
            MenuUrl = "/dashboard",
            Icon = "layout-dashboard",
            Visible = true,
            OrderNo = 0,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Menus.Add(adminDashboardMenu);
    }
    else
    {
        adminDashboardMenu.ParentMenuId = null;
        adminDashboardMenu.MenuName = "Dashboard";
        adminDashboardMenu.MenuUrl = "/dashboard";
        adminDashboardMenu.Icon = "layout-dashboard";
        adminDashboardMenu.Visible = true;
        adminDashboardMenu.OrderNo = 0;
        adminDashboardMenu.IsDeleted = false;
        adminDashboardMenu.UpdatedAt = DateTime.UtcNow;
    }

    var managerDashboardMenu = await db.Menus.FirstOrDefaultAsync(m => m.MenuCode == "DASHBOARD_MANAGER");
    if (managerDashboardMenu == null)
    {
        managerDashboardMenu = new Menu
        {
            MenuCode = "DASHBOARD_MANAGER",
            ParentMenuId = null,
            MenuName = "Dashboard",
            MenuUrl = "/dashboard/manager",
            Icon = "layout-dashboard",
            Visible = true,
            OrderNo = 0,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Menus.Add(managerDashboardMenu);
    }
    else
    {
        managerDashboardMenu.ParentMenuId = null;
        managerDashboardMenu.MenuName = "Dashboard";
        managerDashboardMenu.MenuUrl = "/dashboard/manager";
        managerDashboardMenu.Icon = "layout-dashboard";
        managerDashboardMenu.Visible = true;
        managerDashboardMenu.OrderNo = 0;
        managerDashboardMenu.IsDeleted = false;
        managerDashboardMenu.UpdatedAt = DateTime.UtcNow;
    }

    var employeeDashboardMenu = await db.Menus.FirstOrDefaultAsync(m => m.MenuCode == "DASHBOARD_EMPLOYEE");
    if (employeeDashboardMenu == null)
    {
        employeeDashboardMenu = new Menu
        {
            MenuCode = "DASHBOARD_EMPLOYEE",
            ParentMenuId = null,
            MenuName = "Dashboard",
            MenuUrl = "/dashboard/employee",
            Icon = "layout-dashboard",
            Visible = true,
            OrderNo = 0,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Menus.Add(employeeDashboardMenu);
    }
    else
    {
        employeeDashboardMenu.ParentMenuId = null;
        employeeDashboardMenu.MenuName = "Dashboard";
        employeeDashboardMenu.MenuUrl = "/dashboard/employee";
        employeeDashboardMenu.Icon = "layout-dashboard";
        employeeDashboardMenu.Visible = true;
        employeeDashboardMenu.OrderNo = 0;
        employeeDashboardMenu.IsDeleted = false;
        employeeDashboardMenu.UpdatedAt = DateTime.UtcNow;
    }

    await db.SaveChangesAsync();

    var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin" && !r.IsDeleted);
    if (adminRole != null)
    {
        var adminDashboardAccess = await db.RoleMenus.FirstOrDefaultAsync(rm => rm.RoleId == adminRole.Id && rm.MenuId == adminDashboardMenu.MenuId);
        if (adminDashboardAccess == null)
        {
            db.RoleMenus.Add(new RoleMenu
            {
                RoleId = adminRole.Id,
                MenuId = adminDashboardMenu.MenuId,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            adminDashboardAccess.IsDeleted = false;
            adminDashboardAccess.UpdatedAt = DateTime.UtcNow;
        }
    }

    var managerRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Manager" && !r.IsDeleted);
    if (managerRole != null)
    {
        var managerDashboardAccess = await db.RoleMenus.FirstOrDefaultAsync(rm => rm.RoleId == managerRole.Id && rm.MenuId == managerDashboardMenu.MenuId);
        if (managerDashboardAccess == null)
        {
            db.RoleMenus.Add(new RoleMenu
            {
                RoleId = managerRole.Id,
                MenuId = managerDashboardMenu.MenuId,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            managerDashboardAccess.IsDeleted = false;
            managerDashboardAccess.UpdatedAt = DateTime.UtcNow;
        }
    }

    var employeeRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Employee" && !r.IsDeleted);
    if (employeeRole != null)
    {
        var employeeDashboardAccess = await db.RoleMenus.FirstOrDefaultAsync(rm => rm.RoleId == employeeRole.Id && rm.MenuId == employeeDashboardMenu.MenuId);
        if (employeeDashboardAccess == null)
        {
            db.RoleMenus.Add(new RoleMenu
            {
                RoleId = employeeRole.Id,
                MenuId = employeeDashboardMenu.MenuId,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            employeeDashboardAccess.IsDeleted = false;
            employeeDashboardAccess.UpdatedAt = DateTime.UtcNow;
        }
    }

    await db.SaveChangesAsync();

    await EnsureIssueReportMenuAsync(db);

    // Soft-delete existing tasks that belong to deleted projects
    var orphanedTasks = await db.Tasks
        .Where(t => t.IsDeleted != true && t.Project.IsDeleted == true)
        .ToListAsync();
    foreach (var task in orphanedTasks)
    {
        task.IsDeleted = true;
        task.UpdatedAt = DateTime.UtcNow;
    }
    if (orphanedTasks.Any())
    {
        db.Tasks.UpdateRange(orphanedTasks);
    }

    await db.SaveChangesAsync();
}

static async System.Threading.Tasks.Task EnsureReportUpgradeSchemaAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync(@"
ALTER TABLE ""Issues"" ADD COLUMN IF NOT EXISTS ""DelayReason"" character varying(300);

ALTER TABLE ""Issues"" ADD COLUMN IF NOT EXISTS ""IsBlocked"" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE ""Issues"" ADD COLUMN IF NOT EXISTS ""BlockedBy"" character varying(200);

ALTER TABLE ""Issues"" ADD COLUMN IF NOT EXISTS ""EscalationLevel"" integer NOT NULL DEFAULT 0;
");
}
static async System.Threading.Tasks.Task EnsureIssueReportMenuAsync(AppDbContext db)
{
    var reportsMenu = await db.Menus.FirstOrDefaultAsync(m => m.MenuCode == "REPORTS" && !m.IsDeleted);
    var issueReportMenu = await db.Menus.FirstOrDefaultAsync(m => m.MenuCode == "REPORTS_ISSUES");

    if (issueReportMenu == null)
    {
        issueReportMenu = new Menu
        {
            MenuCode = "REPORTS_ISSUES",
            ParentMenuId = reportsMenu?.MenuId,
            MenuName = "Issue Report",
            MenuUrl = "/reports/issues",
            Icon = "file-pen-line",
            Visible = true,
            OrderNo = 15,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Menus.Add(issueReportMenu);
        await db.SaveChangesAsync();
    }
    else
    {
        issueReportMenu.ParentMenuId = reportsMenu?.MenuId;
        issueReportMenu.MenuName = "Issue Report";
        issueReportMenu.MenuUrl = "/reports/issues";
        issueReportMenu.Icon = "file-pen-line";
        issueReportMenu.Visible = true;
        issueReportMenu.OrderNo = 15;
        issueReportMenu.IsDeleted = false;
        issueReportMenu.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    var reportRoles = await db.Roles
        .Where(role => !role.IsDeleted && (role.Name == "Admin" || role.Name == "Manager" || role.Name == "Employee"))
        .ToListAsync();

    foreach (var role in reportRoles)
    {
        var roleMenu = await db.RoleMenus.FirstOrDefaultAsync(rm => rm.RoleId == role.Id && rm.MenuId == issueReportMenu.MenuId);
        if (roleMenu == null)
        {
            db.RoleMenus.Add(new RoleMenu
            {
                RoleId = role.Id,
                MenuId = issueReportMenu.MenuId,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            roleMenu.IsDeleted = false;
            roleMenu.UpdatedAt = DateTime.UtcNow;
        }
    }

    await db.SaveChangesAsync();
}
