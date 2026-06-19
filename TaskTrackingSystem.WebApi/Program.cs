using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using TaskTrackingSystem.Database.AppDbContextModels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.IdentityModel.Tokens;

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
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Dashboard.DashboardService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Report.ReportService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Menu.MenuService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.UserDevice.UserDeviceService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Notification.FirebaseNotificationService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Notification.NotificationRealtimeService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Features.Notification.NotificationService>();
builder.Services.AddScoped<TaskTrackingSystem.WebApi.Infrastructure.PermissionAuthorizationService>();
builder.Services.AddScoped<IPasswordHasher<TaskTrackingSystem.Database.AppDbContextModels.User>, PasswordHasher<TaskTrackingSystem.Database.AppDbContextModels.User>>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

var app = builder.Build();

await EnsureSeedDataAsync(app);


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowWebApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TaskTrackingSystem.WebApi.Features.Notification.NotificationHub>("/hubs/notifications");

app.Run();

static async System.Threading.Tasks.Task EnsureSeedDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var backlogMenu = await db.Menus.FirstOrDefaultAsync(m => m.MenuCode == "TASKS_BACKLOG");
    if (backlogMenu == null)
    {
        db.Menus.Add(new Menu
        {
            MenuCode = "TASKS_BACKLOG",
            MenuName = "Task Backlog",
            MenuUrl = "/tasks/backlog",
            Icon = "layers",
            Visible = true,
            OrderNo = 25,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        });
    }
    else
    {
        backlogMenu.MenuName = "Task Backlog";
        backlogMenu.MenuUrl = "/tasks/backlog";
        backlogMenu.Icon = "layers";
        backlogMenu.Visible = true;
        backlogMenu.OrderNo = 25;
        backlogMenu.IsDeleted = false;
        backlogMenu.UpdatedAt = DateTime.UtcNow;
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
            Visible = true,
            OrderNo = 0,
            IsDeleted = false,
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
        dashboardMenu.Visible = true;
        dashboardMenu.OrderNo = 0;
        dashboardMenu.IsDeleted = false;
        dashboardMenu.UpdatedAt = DateTime.UtcNow;
    }

    var adminDashboardMenu = await db.Menus.FirstOrDefaultAsync(m => m.MenuCode == "DASHBOARD_ADMIN");
    if (adminDashboardMenu == null)
    {
        adminDashboardMenu = new Menu
        {
            MenuCode = "DASHBOARD_ADMIN",
            ParentMenuId = dashboardMenu.MenuId,
            MenuName = "Admin Dashboard",
            MenuUrl = "/dashboard",
            Icon = "layout-dashboard",
            Visible = true,
            OrderNo = 1,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Menus.Add(adminDashboardMenu);
    }
    else
    {
        adminDashboardMenu.ParentMenuId = dashboardMenu.MenuId;
        adminDashboardMenu.MenuName = "Admin Dashboard";
        adminDashboardMenu.MenuUrl = "/dashboard";
        adminDashboardMenu.Icon = "layout-dashboard";
        adminDashboardMenu.Visible = true;
        adminDashboardMenu.OrderNo = 1;
        adminDashboardMenu.IsDeleted = false;
        adminDashboardMenu.UpdatedAt = DateTime.UtcNow;
    }

    var managerDashboardMenu = await db.Menus.FirstOrDefaultAsync(m => m.MenuCode == "DASHBOARD_MANAGER");
    if (managerDashboardMenu == null)
    {
        managerDashboardMenu = new Menu
        {
            MenuCode = "DASHBOARD_MANAGER",
            ParentMenuId = dashboardMenu.MenuId,
            MenuName = "Manager Dashboard",
            MenuUrl = "/dashboard/manager",
            Icon = "layout-dashboard",
            Visible = true,
            OrderNo = 2,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Menus.Add(managerDashboardMenu);
    }
    else
    {
        managerDashboardMenu.ParentMenuId = dashboardMenu.MenuId;
        managerDashboardMenu.MenuName = "Manager Dashboard";
        managerDashboardMenu.MenuUrl = "/dashboard/manager";
        managerDashboardMenu.Icon = "layout-dashboard";
        managerDashboardMenu.Visible = true;
        managerDashboardMenu.OrderNo = 2;
        managerDashboardMenu.IsDeleted = false;
        managerDashboardMenu.UpdatedAt = DateTime.UtcNow;
    }

    var employeeDashboardMenu = await db.Menus.FirstOrDefaultAsync(m => m.MenuCode == "DASHBOARD_EMPLOYEE");
    if (employeeDashboardMenu == null)
    {
        employeeDashboardMenu = new Menu
        {
            MenuCode = "DASHBOARD_EMPLOYEE",
            ParentMenuId = dashboardMenu.MenuId,
            MenuName = "Employee Dashboard",
            MenuUrl = "/dashboard/employee",
            Icon = "layout-dashboard",
            Visible = true,
            OrderNo = 3,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Menus.Add(employeeDashboardMenu);
    }
    else
    {
        employeeDashboardMenu.ParentMenuId = dashboardMenu.MenuId;
        employeeDashboardMenu.MenuName = "Employee Dashboard";
        employeeDashboardMenu.MenuUrl = "/dashboard/employee";
        employeeDashboardMenu.Icon = "layout-dashboard";
        employeeDashboardMenu.Visible = true;
        employeeDashboardMenu.OrderNo = 3;
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
