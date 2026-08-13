using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using TaskTrackingSystem.WebApp.Localization;
using TaskTrackingSystem.Shared.Localization;
using TaskTrackingSystem.WebApp;
using TaskTrackingSystem.WebApp.Components;

var defaultCulture = CultureInfo.GetCultureInfo("my-MM");
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = true);

// Register HttpClient for WebApi calls
var webApiBaseUrl = builder.Configuration["WebApi:BaseUrl"] ?? "http://127.0.0.1:5001/api/";

var webApiBuilder = builder.Services.AddHttpClient("WebApi", client =>
{
    client.BaseAddress = new Uri(webApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("WebApi:TimeoutSeconds", builder.Environment.IsDevelopment() ? 30 : 60));
});

webApiBuilder.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new SocketsHttpHandler
    {
        ConnectTimeout = TimeSpan.FromSeconds(
            builder.Configuration.GetValue("WebApi:ConnectTimeoutSeconds", builder.Environment.IsDevelopment() ? 5 : 15)),
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    };

    if (builder.Environment.IsDevelopment() &&
        webApiBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
    }

    return handler;
});

builder.Services.AddScoped<UserSessionState>();
builder.Services.AddScoped<ApiClientService>();
builder.Services.AddScoped<MenuAuthorizationService>();
builder.Services.AddScoped<UiLanguageService>();
builder.Services.AddScoped<ILocalizedContentService, LocalizedContentService>();
builder.Services.AddScoped<TaskTrackingSystem.Shared.Localization.IContentTranslationService, ApiContentTranslationService>();
builder.Services.AddScoped<TaskTrackingSystem.Shared.Localization.LanguageDetectionService>();

var supportedCultures = new[]
{
    CultureInfo.GetCultureInfo("my-MM"),
    CultureInfo.GetCultureInfo("en-US")
};

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("my-MM");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

// Cookie authentication for Blazor pages and HTTP middleware.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = AuthenticationSessionDefaults.SessionLifetime;
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddAuthorization();
builder.Services.AddAuthorizationCore();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, CustomAuthenticationStateProvider>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    var catalog = AppLocalization.ValidateCatalogs();
    if (catalog.MissingInEnglish.Count > 0 || catalog.MissingInBurmese.Count > 0 || catalog.DuplicateKeys.Count > 0)
    {
        app.Logger.LogWarning(
            "Localization catalog validation: missing English={MissingEnglish}; missing Burmese={MissingBurmese}; duplicate={Duplicate}",
            string.Join(", ", catalog.MissingInEnglish),
            string.Join(", ", catalog.MissingInBurmese),
            string.Join(", ", catalog.DuplicateKeys));
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAccountEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
