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

// CORS policy for WebApp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp", policy =>
    {
        policy.WithOrigins("http://localhost:5247", "https://localhost:7176")
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
