using System.Security.Claims;
using TaskTrackingSystem.Shared.Models.Menu;

namespace TaskTrackingSystem.WebApp;

public class UserSessionState
{
    public string? Token { get; set; }
    public ClaimsPrincipal? CachedUser { get; set; }

    public string? CachedMenuRoleId { get; set; }
    public List<MenuDto>? CachedMenus { get; set; }

    /// <summary>
    /// The server-side permissions version (latest role access timestamp) when the menus were last fetched.
    /// Used to detect when an admin has changed a role's permissions so the cache can be busted.
    /// </summary>
    public string? CachedMenuVersion { get; set; }

    public void ClearMenuCache()
    {
        CachedMenuRoleId = null;
        CachedMenus = null;
        CachedMenuVersion = null;
    }

    public void ClearSession()
    {
        Token = null;
        CachedUser = null;
        ClearMenuCache();
    }
}
