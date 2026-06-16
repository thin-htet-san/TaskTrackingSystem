using System.Security.Claims;
using TaskTrackingSystem.Shared.Models.Menu;

namespace TaskTrackingSystem.WebApp;

public class UserSessionState
{
    public string? Token { get; set; }
    public ClaimsPrincipal? CachedUser { get; set; }

    public string? CachedAccessRoleId { get; set; }
    public List<MenuDto>? CachedAccessItems { get; set; }
    public HashSet<string>? CachedAccessCodes { get; set; }

    /// <summary>
    /// The server-side permissions version (latest role access timestamp) when the access items were last fetched.
    /// Used to detect when an admin has changed a role's permissions so the cache can be busted.
    /// </summary>
    public string? CachedAccessVersion { get; set; }

    public void ClearAccessCache()
    {
        CachedAccessRoleId = null;
        CachedAccessItems = null;
        CachedAccessCodes = null;
        CachedAccessVersion = null;
    }

    public void ClearSession()
    {
        Token = null;
        CachedUser = null;
        ClearAccessCache();
    }
}

