using System.Security.Claims;
using TaskTrackingSystem.Shared.Models.Menu;

namespace TaskTrackingSystem.WebApp;

public class UserSessionState
{
    public string? Token { get; set; }
    public ClaimsPrincipal? CachedUser { get; set; }

    public string? CachedMenuRoleId { get; set; }
    public List<MenuDto>? CachedMenus { get; set; }

    public void ClearMenuCache()
    {
        CachedMenuRoleId = null;
        CachedMenus = null;
    }

    public void ClearSession()
    {
        Token = null;
        CachedUser = null;
        ClearMenuCache();
    }
}
