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
using TaskTrackingSystem.Shared;

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
    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                Result.Failure("Your session has expired. Please log out and log in again.", 401));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                Result.Failure("You do not have permission to perform this action.", 403));
        }
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
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                Result.Failure("The server hit an error while processing the request.", 500));
        });
    });
}

app.UseCors("AllowWebApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/version", () => Results.Ok(new
{
    status = "ok",
    build = "auth-json-a93c81d",
    timestamp = "2026-07-24T06:35:00Z"
}));
app.MapGet("/api/version", () => Results.Ok(new
{
    status = "ok",
    build = "cached-token-3438c94",
    timestamp = "2026-07-24T06:45:00Z"
}));
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

    await ResetPostgresSequencesAsync(db);
}

static async System.Threading.Tasks.Task ResetPostgresSequencesAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync(@"
DO $$
DECLARE
    sequence_record RECORD;
    max_value BIGINT;
BEGIN
    FOR sequence_record IN
        SELECT
            sequence_namespace.nspname AS sequence_schema,
            sequence_class.relname AS sequence_name,
            table_namespace.nspname AS table_schema,
            table_class.relname AS table_name,
            table_attribute.attname AS column_name
        FROM pg_class sequence_class
        JOIN pg_namespace sequence_namespace ON sequence_namespace.oid = sequence_class.relnamespace
        JOIN pg_depend dependency ON dependency.objid = sequence_class.oid
        JOIN pg_class table_class ON table_class.oid = dependency.refobjid
        JOIN pg_namespace table_namespace ON table_namespace.oid = table_class.relnamespace
        JOIN pg_attribute table_attribute
            ON table_attribute.attrelid = table_class.oid
            AND table_attribute.attnum = dependency.refobjsubid
        WHERE sequence_class.relkind = 'S'
          AND table_namespace.nspname = 'public'
          AND dependency.deptype IN ('a', 'i')
    LOOP
        EXECUTE format(
            'SELECT MAX(%I)::bigint FROM %I.%I',
            sequence_record.column_name,
            sequence_record.table_schema,
            sequence_record.table_name)
        INTO max_value;

        IF max_value IS NULL THEN
            EXECUTE format(
                'SELECT setval(%L::regclass, 1, false)',
                format('%I.%I', sequence_record.sequence_schema, sequence_record.sequence_name));
        ELSE
            EXECUTE format(
                'SELECT setval(%L::regclass, %s, true)',
                format('%I.%I', sequence_record.sequence_schema, sequence_record.sequence_name),
                max_value);
        END IF;
    END LOOP;
END $$;
");
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
