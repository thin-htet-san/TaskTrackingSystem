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

    var widgetSetupMenu = await db.Menus.FirstOrDefaultAsync(m => m.MenuCode == "DASHBOARD_WIDGETS");
    if (widgetSetupMenu == null)
    {
        widgetSetupMenu = new Menu
        {
            MenuCode = "DASHBOARD_WIDGETS",
            MenuName = "Widget Setup",
            MenuUrl = "/widget-setup",
            Icon = "layout-grid",
            Visible = true,
            OrderNo = 26,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Menus.Add(widgetSetupMenu);
        await db.SaveChangesAsync();
    }
    else
    {
        widgetSetupMenu.MenuName = "Widget Setup";
        widgetSetupMenu.MenuUrl = "/widget-setup";
        widgetSetupMenu.Icon = "layout-grid";
        widgetSetupMenu.Visible = true;
        widgetSetupMenu.OrderNo = 26;
        widgetSetupMenu.IsDeleted = false;
        widgetSetupMenu.UpdatedAt = DateTime.UtcNow;
    }

    var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin" && !r.IsDeleted);
    if (adminRole != null && widgetSetupMenu.MenuId > 0)
    {
        var adminMenuAccess = await db.RoleMenus.FirstOrDefaultAsync(rm => rm.RoleId == adminRole.Id && rm.MenuId == widgetSetupMenu.MenuId);
        if (adminMenuAccess == null)
        {
            db.RoleMenus.Add(new RoleMenu
            {
                RoleId = adminRole.Id,
                MenuId = widgetSetupMenu.MenuId,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            adminMenuAccess.IsDeleted = false;
            adminMenuAccess.UpdatedAt = DateTime.UtcNow;
        }
    }

    var widgetSeeds = new[]
    {
        new DashboardWidget { WidgetCode = "WIDGET_GREETING_BANNER", WidgetName = "Greeting Banner", Description = "A friendly greeting panel with the current date.", Category = "Overview", ComponentKey = "GreetingBanner", DataSourceKey = "CurrentUser", DefaultWidth = 4, DefaultHeight = 2, DefaultOrder = 1, IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow },
        new DashboardWidget { WidgetCode = "WIDGET_KPI_CARDS", WidgetName = "KPI Cards", Description = "Quick counts for users, projects, and tasks.", Category = "Overview", ComponentKey = "KpiCards", DataSourceKey = "DashboardSummary", DefaultWidth = 4, DefaultHeight = 2, DefaultOrder = 2, IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow },
        new DashboardWidget { WidgetCode = "WIDGET_CALENDAR_STRIP", WidgetName = "Calendar Strip", Description = "A compact weekly view with due tasks.", Category = "Planning", ComponentKey = "CalendarStrip", DataSourceKey = "DueTasks", DefaultWidth = 4, DefaultHeight = 3, DefaultOrder = 3, IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow },
        new DashboardWidget { WidgetCode = "WIDGET_PROJECT_PROGRESS", WidgetName = "Project Progress", Description = "Shows completion progress for active projects.", Category = "Analytics", ComponentKey = "ProjectProgressChart", DataSourceKey = "ProjectProgress", DefaultWidth = 4, DefaultHeight = 3, DefaultOrder = 4, IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow },
        new DashboardWidget { WidgetCode = "WIDGET_TIMELINE", WidgetName = "Timeline", Description = "Highlights project timing and start/end dates.", Category = "Planning", ComponentKey = "TimelineChart", DataSourceKey = "Projects", DefaultWidth = 4, DefaultHeight = 3, DefaultOrder = 5, IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow },
        new DashboardWidget { WidgetCode = "WIDGET_TASK_TRAY", WidgetName = "Task Tray", Description = "A small queue of work the user can pick up next.", Category = "Work", ComponentKey = "TaskTray", DataSourceKey = "Tasks", DefaultWidth = 4, DefaultHeight = 3, DefaultOrder = 6, IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow },
        new DashboardWidget { WidgetCode = "WIDGET_WORKLOAD", WidgetName = "Workload", Description = "Team load distribution across members.", Category = "Analytics", ComponentKey = "WorkloadChart", DataSourceKey = "ProjectsAndUsers", DefaultWidth = 4, DefaultHeight = 3, DefaultOrder = 7, IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow },
        new DashboardWidget { WidgetCode = "WIDGET_AUDIT_FEED", WidgetName = "Audit Feed", Description = "Recent administrative activity.", Category = "Admin", ComponentKey = "AuditFeed", DataSourceKey = "AuditLogs", DefaultWidth = 4, DefaultHeight = 3, DefaultOrder = 8, IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow }
    };

    var widgetAccessTemplates = new Dictionary<string, (bool admin, bool manager, bool employee)>(StringComparer.OrdinalIgnoreCase)
    {
        ["WIDGET_GREETING_BANNER"] = (true, true, true),
        ["WIDGET_KPI_CARDS"] = (true, true, true),
        ["WIDGET_CALENDAR_STRIP"] = (true, true, true),
        ["WIDGET_PROJECT_PROGRESS"] = (true, true, false),
        ["WIDGET_TIMELINE"] = (true, true, false),
        ["WIDGET_TASK_TRAY"] = (true, true, true),
        ["WIDGET_WORKLOAD"] = (true, true, false),
        ["WIDGET_AUDIT_FEED"] = (true, false, false)
    };

    var roles = await db.Roles.Where(r => !r.IsDeleted).ToListAsync();

    foreach (var seed in widgetSeeds)
    {
        var existingWidget = await db.DashboardWidgets.FirstOrDefaultAsync(w => w.WidgetCode == seed.WidgetCode);
        if (existingWidget == null)
        {
            db.DashboardWidgets.Add(seed);
            await db.SaveChangesAsync();
            existingWidget = seed;
        }
        else
        {
            existingWidget.WidgetName = seed.WidgetName;
            existingWidget.Description = seed.Description;
            existingWidget.Category = seed.Category;
            existingWidget.ComponentKey = seed.ComponentKey;
            existingWidget.DataSourceKey = seed.DataSourceKey;
            existingWidget.DefaultWidth = seed.DefaultWidth;
            existingWidget.DefaultHeight = seed.DefaultHeight;
            existingWidget.DefaultOrder = seed.DefaultOrder;
            existingWidget.IsActive = seed.IsActive;
            existingWidget.IsDeleted = false;
            existingWidget.UpdatedAt = DateTime.UtcNow;
        }

        var template = widgetAccessTemplates[seed.WidgetCode];
        foreach (var role in roles)
        {
            var shouldView = role.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                ? template.admin
                : role.Name.Equals("Manager", StringComparison.OrdinalIgnoreCase)
                    ? template.manager
                    : template.employee;

            var existingAccess = await db.RoleDashboardWidgets.FirstOrDefaultAsync(row => row.RoleId == role.Id && row.WidgetId == existingWidget.WidgetId);
            if (existingAccess == null)
            {
                db.RoleDashboardWidgets.Add(new RoleDashboardWidget
                {
                    RoleId = role.Id,
                    WidgetId = existingWidget.WidgetId,
                    CanView = shouldView,
                    CanConfigure = role.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase),
                    IsDefaultVisible = shouldView,
                    DefaultGridX = 0,
                    DefaultGridY = 0,
                    DefaultWidth = seed.DefaultWidth,
                    DefaultHeight = seed.DefaultHeight,
                    DefaultSortOrder = seed.DefaultOrder,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existingAccess.CanView = shouldView;
                existingAccess.CanConfigure = role.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase);
                existingAccess.IsDefaultVisible = shouldView;
                existingAccess.DefaultWidth = seed.DefaultWidth;
                existingAccess.DefaultHeight = seed.DefaultHeight;
                existingAccess.DefaultSortOrder = seed.DefaultOrder;
                existingAccess.IsDeleted = false;
                existingAccess.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

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
