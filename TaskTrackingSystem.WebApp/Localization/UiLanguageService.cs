using System.Globalization;
using Microsoft.JSInterop;
using TaskTrackingSystem.Shared.Localization;

namespace TaskTrackingSystem.WebApp.Localization;

public enum AppLanguage
{
    English,
    Burmese
}

public sealed class UiTextBundle
{
    public required string Workspace { get; init; }
    public required string SignIn { get; init; }
    public required string SignOut { get; init; }
    public required string SignOutTitle { get; init; }
    public required string SignOutDescription { get; init; }
    public required string Cancel { get; init; }
    public required string ConfirmSignOut { get; init; }
    public required string AccessDenied { get; init; }
    public required string AccessDeniedDescription { get; init; }
    public required string BackToDashboard { get; init; }
    public required string NoAccessItems { get; init; }
    public required string AuditLogs { get; init; }
    public required string LanguageEnglish { get; init; }
    public required string LanguageBurmese { get; init; }
    public required string LanguageTitle { get; init; }
}

public sealed class UiLanguageService
{
    private const string StorageKey = "tts-language";
    private readonly IJSRuntime _js;

    public UiLanguageService(IJSRuntime js)
    {
        _js = js;
    }

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Burmese;

    public UiTextBundle Texts => new()
    {
        Workspace = AppLocalization.Text("common.workspace", "Workspace"),
        SignIn = AppLocalization.Text("common.signIn", "Sign In"),
        SignOut = AppLocalization.Text("common.signOut", "Sign Out"),
        SignOutTitle = AppLocalization.Text("common.signOutTitle", "Sign Out?"),
        SignOutDescription = AppLocalization.Text("common.signOutDescription", "Are you sure you want to sign out of Taskify?"),
        Cancel = AppLocalization.Text("common.cancel", "Cancel"),
        ConfirmSignOut = AppLocalization.Text("common.confirmSignOut", "Yes, Sign Out"),
        AccessDenied = AppLocalization.Text("common.accessDenied", "Access Denied"),
        AccessDeniedDescription = AppLocalization.Text("common.accessDeniedDescription", "You do not have permission to access this page. Please contact your system administrator."),
        BackToDashboard = AppLocalization.Text("common.backToDashboard", "Back to Dashboard"),
        NoAccessItems = AppLocalization.Text("common.noAccessItems", "No access items are available for this role."),
        AuditLogs = AppLocalization.Text("page.auditLogs", "Audit Logs"),
        LanguageEnglish = AppLocalization.Text("common.languageEnglish", "English"),
        LanguageBurmese = AppLocalization.Text("common.languageBurmese", "Burmese"),
        LanguageTitle = AppLocalization.Text("common.currentLanguage", "Switch language")
    };

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        var code = await _js.InvokeAsync<string?>("languagePreference.init", StorageKey);
        SetLanguageFromCode(code);
    }

    public async Task ToggleAsync()
    {
        var code = await _js.InvokeAsync<string?>("languagePreference.toggle", StorageKey);
        SetLanguageFromCode(code);
    }

    public async Task SetAsync(AppLanguage language)
    {
        var code = ToCode(language);
        await _js.InvokeVoidAsync("languagePreference.set", StorageKey, code);
        SetLanguageFromCode(code);
    }

    public string GetCode() => ToCode(CurrentLanguage);

    public static string ToCode(AppLanguage language) => language == AppLanguage.Burmese ? "my-MM" : "en-US";

    public static AppLanguage FromCode(string? code) =>
        string.Equals(code, "en", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(code, "en-US", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.English
            : AppLanguage.Burmese;

    public string T(string key, string fallback = "") => AppLocalization.Text(key, fallback);
    public string PageTitle(string key, string fallback = "") => AppLocalization.PageTitle(key, fallback);
    public string PageDescription(string key, string fallback = "") => AppLocalization.PageDescription(key, fallback);
    public string StatusLabel(TaskTrackingSystem.Shared.Enums.AppTaskStatus status) => AppLocalization.StatusLabel(status);
    public string PriorityLabel(TaskTrackingSystem.Shared.Enums.TaskPriority priority) => AppLocalization.PriorityLabel(priority);
    public string EscalationLabel(int level) => AppLocalization.EscalationLabel(level);

    private void SetLanguageFromCode(string? code)
    {
        var next = FromCode(code);
        ApplyCulture(next);

        if (next == CurrentLanguage)
        {
            return;
        }

        CurrentLanguage = next;
        Changed?.Invoke();
    }

    private static void ApplyCulture(AppLanguage language)
    {
        var culture = language == AppLanguage.Burmese ? CultureInfo.GetCultureInfo("my-MM") : CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
