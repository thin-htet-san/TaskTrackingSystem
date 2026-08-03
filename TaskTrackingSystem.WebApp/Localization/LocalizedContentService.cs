using TaskTrackingSystem.Shared.Models.Issue;
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

        return !string.IsNullOrWhiteSpace(english) ? english : burmese ?? string.Empty;
    }

    public string GetFullName(string? firstNameEnglish, string? lastNameEnglish, string? firstNameBurmese, string? lastNameBurmese)
    {
        var english = JoinName(firstNameEnglish, lastNameEnglish);
        var burmese = JoinName(firstNameBurmese, lastNameBurmese);
        return GetText(english, burmese);
    }

    public string GetProjectName(ProjectDto project) => GetText(project.Name, project.NameMy);
    public string GetProjectDescription(ProjectDto project) => GetText(project.Description, project.DescriptionMy);
    public string GetTaskTitle(TaskDto task) => GetText(task.Title, task.TitleMy);
    public string GetTaskDescription(TaskDto task) => GetText(task.Description, task.DescriptionMy);
    public string GetIssueTitle(IssueDto issue) => GetText(issue.Title, issue.TitleMy);
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
}
