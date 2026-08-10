using TaskTrackingSystem.Shared.Models.Issue;
using TaskTrackingSystem.Shared.Localization;
using TaskTrackingSystem.Shared.Models.Menu;
using TaskTrackingSystem.Shared.Models.Project;
using TaskTrackingSystem.Shared.Models.Role;
using TaskTrackingSystem.Shared.Models.Task;
using TaskTrackingSystem.Shared.Models.User;

namespace TaskTrackingSystem.WebApp.Localization;

public interface ILocalizedContentService
{
    string GetText(string? english, string? burmese);

    string GetFullName(
        string? firstNameEnglish,
        string? lastNameEnglish,
        string? firstNameBurmese,
        string? lastNameBurmese);

    string GetProjectName(ProjectDto project);
    string GetProjectDescription(ProjectDto project);
    string GetTaskTitle(TaskDto task);
    string GetTaskDescription(TaskDto task);
    string GetIssueTitle(IssueDto issue);
    string GetIssueDescription(IssueDto issue);
    string GetMenuName(MenuDto menu);
    string GetMenuName(AccessMenuDto menu);
    string GetRoleName(RoleDto role);
    string GetPermissionName(string? actionNameEnglish, string? actionNameBurmese);
    string GetUserFullName(UserDto user);
}

public sealed class LocalizedContentService : ILocalizedContentService
{
    private readonly UiLanguageService _uiLanguageService;
    private readonly LanguageDetectionService _languageDetection = new();
    private static readonly IReadOnlyDictionary<string, string> KnownBurmeseNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Kyaw Kyaw Aung"] = "ကျော်ကျော် အောင်",
        ["Min Min Htun"] = "မင်းမင်း ထွန်း",
        ["Mg Mg Naing"] = "မောင်မောင် နိုင်",
        ["Aung Aung Oo"] = "အောင်အောင် ဦး",
        ["Wai Yan Tun"] = "ဝေယံ ထွန်း",
        ["Thura Ko"] = "သူရ ကို",
        ["Su Su Win"] = "စုစု ဝင်း",
        ["Nandar Hlaing"] = "နန္ဒာ လှိုင်",
        ["Phyo Phyo Aye"] = "ဖြိုးဖြိုး အေး",
        ["Zaw Zaw Lin"] = "ဇော်ဇော် လင်း",
        ["Hla Hla Khin"] = "လှလှ ခင်",
        ["Nwe Nwe Aye"] = "နွေနွေ အေး",
        ["Ei Ei Mon"] = "အိအိ မွန်",
        ["Thandar Win"] = "သန္တာ ဝင်း",
        ["Khin Khin Tun"] = "ခင်ခင် ထွန်း",
        ["Su Mon Hlaing"] = "စုမွန် လှိုင်",
        ["Mon Mon Aung"] = "မွန်မွန် အောင်",
        ["Yee Yee Lwin"] = "ရီရီ လွင်",
        ["Pyae Pyae Naing"] = "ပြည့်ပြည့် နိုင်",
        ["May Zaw"] = "မေ ဇော်",
        ["Htet Htet Oo"] = "ထက်ထက် ဦး",
        ["Aye Aye Khaing"] = "အေးအေး ခိုင်",
        ["Chaw Chaw Aung"] = "ချောချော အောင်",
        ["Mya Mya Myint"] = "မြမြ မြင့်",
        ["Lwin Lwin Htun"] = "လွင်လွင် ထွန်း",
        ["Phyu Phyu Soe"] = "ဖြူဖြူ စိုး",
        ["Myat Myat Aung"] = "မြတ်မြတ် အောင်",
        ["Nyein Nyein Win"] = "ငြိမ်းငြိမ်း ဝင်း",
        ["Kyu Kyu Hlaing"] = "ကြူကြူ လှိုင်",
        ["San San Aye"] = "စန်းစန်း အေး",
        ["Si Si Ko"] = "စီစီ ကို",
        ["Moe Moe Thant"] = "မိုးမိုး သန့်",
        ["Yu Yu Naing"] = "ယုယု နိုင်",
        ["Pan Pan Wai"] = "ပန်းပန်း ဝေ",
        ["Khaing Khine"] = "ခိုင် ခိုင်",
        ["Thiri Aung"] = "သီရိ အောင်",
        ["Hnin Aye"] = "နှင်း အေး",
        ["Wint Oo"] = "ဝင့် ဦး",
        ["Chan Moe"] = "ချမ်း မိုး",
        ["Aye Mon"] = "အေးမွန်",
        ["Htoo Aung Lwin"] = "ထူးအောင် လွင်",
        ["Thin Htet San"] = "သင်းထက် စံ",
        ["Maung Aye"] = "မောင် အေး"
    };

    public LocalizedContentService(UiLanguageService uiLanguageService)
    {
        _uiLanguageService = uiLanguageService;
    }

    public string GetText(string? english, string? burmese)
    {
        if (_uiLanguageService.CurrentLanguage == AppLanguage.Burmese)
        {
            return !string.IsNullOrWhiteSpace(burmese) ? burmese : english ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(english))
        {
            return string.Empty;
        }

        return _languageDetection.AppearsPredominantlyBurmese(english)
            ? string.Empty
            : english;
    }

    public string GetFullName(string? firstNameEnglish, string? lastNameEnglish, string? firstNameBurmese, string? lastNameBurmese)
    {
        var english = JoinName(firstNameEnglish, lastNameEnglish);
        var burmese = JoinName(firstNameBurmese, lastNameBurmese);

        // These are standard system display labels, not personal names.
        if (string.Equals(english, "System Admin", StringComparison.OrdinalIgnoreCase))
        {
            return AppLocalization.Text("common.systemAdmin", "System Admin");
        }

        if (string.Equals(english, "User", StringComparison.OrdinalIgnoreCase))
        {
            return AppLocalization.Text("common.user", "User");
        }

        if (_uiLanguageService.CurrentLanguage == AppLanguage.Burmese
            && string.IsNullOrWhiteSpace(burmese)
            && KnownBurmeseNames.TryGetValue(english, out var knownBurmeseName))
        {
            return knownBurmeseName;
        }

        return GetText(english, burmese);
    }

    public string GetProjectName(ProjectDto project) => WithFallback(GetText(project.Name, project.NameMy), AppLocalization.Text("project.untitledProject", "Untitled project"));
    public string GetProjectDescription(ProjectDto project) => GetText(project.Description, project.DescriptionMy);
    public string GetTaskTitle(TaskDto task) => WithFallback(GetText(task.Title, task.TitleMy), AppLocalization.Text("task.untitledTask", "Untitled task"));
    public string GetTaskDescription(TaskDto task) => GetText(task.Description, task.DescriptionMy);
    public string GetIssueTitle(IssueDto issue) => WithFallback(GetText(issue.Title, issue.TitleMy), AppLocalization.Text("issue.untitledIssue", "Untitled issue"));
    public string GetIssueDescription(IssueDto issue) => GetText(issue.Description, issue.DescriptionMy);
    public string GetMenuName(MenuDto menu) => GetText(menu.MenuName, menu.MenuNameMy);
    public string GetMenuName(AccessMenuDto menu) => GetText(menu.MenuName, menu.MenuNameMy);
    public string GetRoleName(RoleDto role) => GetText(role.Name, role.NameMy);
    public string GetPermissionName(string? actionNameEnglish, string? actionNameBurmese) => GetText(actionNameEnglish, actionNameBurmese);
    public string GetUserFullName(UserDto user) => GetFullName(user.FirstName, user.LastName, user.FirstNameMy, user.LastNameMy);

    private static string JoinName(string? firstName, string? lastName)
    {
        return string.Join(" ", new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
    }

    private static string WithFallback(string text, string fallback) =>
        string.IsNullOrWhiteSpace(text) ? fallback : text;
}
