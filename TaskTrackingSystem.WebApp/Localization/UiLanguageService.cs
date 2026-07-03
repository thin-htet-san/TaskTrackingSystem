using Microsoft.JSInterop;

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

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.English;

    public UiTextBundle Texts => CurrentLanguage == AppLanguage.Burmese
        ? BurmeseTexts
        : EnglishTexts;

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

    public static string ToCode(AppLanguage language) => language == AppLanguage.Burmese ? "my" : "en";

    public static AppLanguage FromCode(string? code) =>
        string.Equals(code, "my", StringComparison.OrdinalIgnoreCase) ? AppLanguage.Burmese : AppLanguage.English;

    private void SetLanguageFromCode(string? code)
    {
        var next = FromCode(code);
        if (next == CurrentLanguage)
        {
            return;
        }

        CurrentLanguage = next;
        Changed?.Invoke();
    }

    private static readonly UiTextBundle EnglishTexts = new()
    {
        Workspace = "Workspace",
        SignIn = "Sign In",
        SignOut = "Sign Out",
        SignOutTitle = "Sign Out?",
        SignOutDescription = "Are you sure you want to sign out of Taskify?",
        Cancel = "Cancel",
        ConfirmSignOut = "Yes, Sign Out",
        AccessDenied = "Access Denied",
        AccessDeniedDescription = "You do not have permission to access this page. Please contact your system administrator.",
        BackToDashboard = "Back to Dashboard",
        NoAccessItems = "No access items are available for this role.",
        AuditLogs = "Audit Logs",
        LanguageEnglish = "English",
        LanguageBurmese = "Burmese",
        LanguageTitle = "Switch language"
    };

    private static readonly UiTextBundle BurmeseTexts = new()
    {
        Workspace = "အလုပ်ခွင်",
        SignIn = "အကောင့်ဝင်မည်",
        SignOut = "အကောင့်ထွက်မည်",
        SignOutTitle = "အကောင့်ထွက်မလား?",
        SignOutDescription = "Taskify မှ အကောင့်ထွက်ရန် သေချာပါသလား?",
        Cancel = "မလုပ်တော့ပါ",
        ConfirmSignOut = "ဟုတ်ကဲ့၊ အကောင့်ထွက်မည်",
        AccessDenied = "ဝင်ခွင့်မရှိပါ",
        AccessDeniedDescription = "ဤစာမျက်နှာကို ဝင်ရောက်ခွင့် မရှိပါ။ စနစ်စီမံခန့်ခွဲသူကို ဆက်သွယ်ပါ။",
        BackToDashboard = "ဒက်ရှ်ဘုတ်သို့ ပြန်သွားမည်",
        NoAccessItems = "ဤအခန်းကဏ္ဍအတွက် ဝင်ခွင့်မရသည့် မီနူးမရှိပါ။",
        AuditLogs = "မှတ်တမ်းများ",
        LanguageEnglish = "အင်္ဂလိပ်",
        LanguageBurmese = "မြန်မာ",
        LanguageTitle = "ဘာသာစကားပြောင်းရန်"
    };
}
