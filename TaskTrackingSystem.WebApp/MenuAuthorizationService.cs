using System.Net.Http.Json;
using System.Security.Claims;
using TaskTrackingSystem.Shared.Models.Menu;

namespace TaskTrackingSystem.WebApp;

public class MenuAuthorizationService
{
    private static readonly HashSet<string> PublicRouteSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty,
        "login",
        "register",
        "error",
        "reset-password"
    };

    private readonly ApiClientService _apiClient;
    private readonly UserSessionState _sessionState;
    private Task<List<MenuDto>>? _loadingAccessItems;
    private Task<HashSet<string>>? _loadingAccessCodes;

    public MenuAuthorizationService(ApiClientService apiClient, UserSessionState sessionState)
    {
        _apiClient = apiClient;
        _sessionState = sessionState;
    }

    public static bool IsPublicRoute(string relativePath)
    {
        return PublicRouteSegments.Contains(GetFirstSegment(relativePath));
    }

    public static string GetFirstSegment(string relativePath)
    {
        var path = relativePath.Split('?')[0].Trim('/');
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        return path.Split('/')[0];
    }

    public static string NormalizeMenuHref(string? menuUrl)
    {
        if (string.IsNullOrWhiteSpace(menuUrl))
        {
            return "/dashboard";
        }

        return menuUrl.StartsWith('/') ? menuUrl : $"/{menuUrl.TrimStart('/')}";
    }

    public bool IsRouteAllowed(ClaimsPrincipal user, string relativePath)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return IsPublicRoute(relativePath);
        }

        if (IsPublicRoute(relativePath))
        {
            return true;
        }

        var cleanRelative = relativePath.Split('?')[0].Trim('/');
        if (cleanRelative.Equals("audit-logs", StringComparison.OrdinalIgnoreCase))
        {
            return user.IsInRole("Admin");
        }

        var roleKey = GetRoleCacheKey(user);

        if (_sessionState.CachedAccessRoleId == roleKey && _sessionState.CachedAccessItems != null)
        {
            return IsRouteAllowed(_sessionState.CachedAccessItems, relativePath);
        }

        return false;
    }

    public bool IsRouteAllowed(IReadOnlyList<MenuDto> accessItems, string relativePath)
    {
        var cleanRelative = relativePath.Split('?')[0].Trim('/');
        var firstSegment = cleanRelative.Split('/')[0];

        if (PublicRouteSegments.Contains(firstSegment))
        {
            return true;
        }

        // Special case for Task Details page route parameter wildcard
        if (cleanRelative.StartsWith("tasks/", StringComparison.OrdinalIgnoreCase) && 
            cleanRelative.EndsWith("/details", StringComparison.OrdinalIgnoreCase))
        {
            var segments = cleanRelative.Split('/');
            if (segments.Length == 3 && long.TryParse(segments[1], out _))
            {
                var hasTasksAccess = accessItems.Any(m => 
                    m.MenuCode.Equals("TASKS_LIST", StringComparison.OrdinalIgnoreCase) ||
                    m.MenuCode.Equals("TASKS_BOARD", StringComparison.OrdinalIgnoreCase) ||
                    m.SubMenus.Any(sm => sm.MenuCode.Equals("TASKS_LIST", StringComparison.OrdinalIgnoreCase) ||
                                         sm.MenuCode.Equals("TASKS_BOARD", StringComparison.OrdinalIgnoreCase)));
                if (hasTasksAccess)
                {
                    return true;
                }
            }
        }

        // Special case for project-specific Kanban board
        if (cleanRelative.StartsWith("projects/", StringComparison.OrdinalIgnoreCase) && 
            cleanRelative.EndsWith("/tasks", StringComparison.OrdinalIgnoreCase))
        {
            var segments = cleanRelative.Split('/');
            if (segments.Length == 3 && long.TryParse(segments[1], out _))
            {
                var hasBoardAccess = accessItems.Any(m => 
                    m.MenuCode.Equals("TASKS_BOARD", StringComparison.OrdinalIgnoreCase) ||
                    m.SubMenus.Any(sm => sm.MenuCode.Equals("TASKS_BOARD", StringComparison.OrdinalIgnoreCase)));
                if (hasBoardAccess)
                {
                    return true;
                }
            }
        }

        // Global board route
        if (cleanRelative.Equals("board", StringComparison.OrdinalIgnoreCase))
        {
            var hasBoardAccess = accessItems.Any(m =>
                m.MenuCode.Equals("TASKS_BOARD", StringComparison.OrdinalIgnoreCase) ||
                m.SubMenus.Any(sm => sm.MenuCode.Equals("TASKS_BOARD", StringComparison.OrdinalIgnoreCase)));

            if (hasBoardAccess)
            {
                return true;
            }
        }

        if (string.IsNullOrEmpty(cleanRelative) || 
            cleanRelative.Equals("dashboard", StringComparison.OrdinalIgnoreCase) ||
            cleanRelative.Equals("home", StringComparison.OrdinalIgnoreCase))
        {
            return MenuCollectionMatchesRoute(accessItems, "dashboard") || 
                   MenuCollectionMatchesRoute(accessItems, "");
        }

        return MenuCollectionMatchesRoute(accessItems, cleanRelative);
    }

    public Task<List<MenuDto>> GetUserMenusAsync(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(new List<MenuDto>());
        }

        var roleKey = GetRoleCacheKey(user);

        if (_sessionState.CachedAccessRoleId == roleKey && _sessionState.CachedAccessItems != null)
        {
            // Cache hit, but verify the access has not been changed by an admin.
            return LoadAccessItemsWithVersionCheckAsync(user, roleKey);
        }

        return LoadAccessItemsAsync(user, roleKey);
    }

    public async Task<bool> HasAccessCodeAsync(ClaimsPrincipal user, string accessCode)
    {
        if (string.IsNullOrWhiteSpace(accessCode))
        {
            return false;
        }

        var codes = await GetCurrentAccessCodesAsync(user);
        return codes.Contains(accessCode.Trim());
    }

    public Task<HashSet<string>> GetCurrentAccessCodesAsync(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        var roleKey = GetRoleCacheKey(user);

        if (_sessionState.CachedAccessRoleId == roleKey && _sessionState.CachedAccessCodes != null)
        {
            return LoadAccessCodesWithVersionCheckAsync(user, roleKey);
        }

        return LoadAccessCodesAsync(user, roleKey);
    }

    public void PreloadMenus(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var roleKey = GetRoleCacheKey(user);
        if (_sessionState.CachedAccessRoleId == roleKey && _sessionState.CachedAccessItems != null)
        {
            return;
        }

        _ = LoadAccessItemsAsync(user, roleKey);
    }

    /// <summary>
    /// When access items are cached, quickly checks the server-side version to see if an admin changed permissions.
    /// If the version changed, busts the cache and re-fetches the full menu list.
    /// Falls back to the cached access items if the version check itself fails (e.g. network error).
    /// </summary>
    private async Task<List<MenuDto>> LoadAccessItemsWithVersionCheckAsync(ClaimsPrincipal user, string roleKey)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var client = _apiClient.CreateClient(user);
            var response = await client.GetAsync("Menu/version", cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var serverVersion = await response.Content.ReadAsStringAsync(cts.Token);
                serverVersion = serverVersion.Trim('"');

                if (serverVersion != _sessionState.CachedAccessVersion)
                {
                    Console.WriteLine($"[MenuAuth] Access version changed ({_sessionState.CachedAccessVersion} -> {serverVersion}). Reloading access items.");
                    _sessionState.ClearAccessCache();
                    return await LoadAccessItemsAsync(user, roleKey);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MenuAuth] Access version check failed (using cache): {ex.Message}");
        }

        return _sessionState.CachedAccessItems!;
    }

    private Task<List<MenuDto>> LoadAccessItemsAsync(ClaimsPrincipal user, string roleKey)
    {
        if (_loadingAccessItems is { IsCompleted: false })
        {
            return _loadingAccessItems;
        }

        _loadingAccessItems = FetchAccessItemsFromApiAsync(user, roleKey);
        return _loadingAccessItems;
    }

    private async Task<HashSet<string>> LoadAccessCodesWithVersionCheckAsync(ClaimsPrincipal user, string roleKey)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var client = _apiClient.CreateClient(user);
            var response = await client.GetAsync("Menu/version", cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var serverVersion = await response.Content.ReadAsStringAsync(cts.Token);
                serverVersion = serverVersion.Trim('"');

                if (serverVersion != _sessionState.CachedAccessVersion)
                {
                    _sessionState.ClearAccessCache();
                    return await LoadAccessCodesAsync(user, roleKey);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MenuAuth] Access code version check failed (using cache): {ex.Message}");
        }

        return _sessionState.CachedAccessCodes ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private Task<HashSet<string>> LoadAccessCodesAsync(ClaimsPrincipal user, string roleKey)
    {
        if (_loadingAccessCodes is { IsCompleted: false })
        {
            return _loadingAccessCodes;
        }

        _loadingAccessCodes = FetchAccessCodesFromApiAsync(user, roleKey);
        return _loadingAccessCodes;
    }

    private async Task<HashSet<string>> FetchAccessCodesFromApiAsync(ClaimsPrincipal user, string roleKey)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var client = _apiClient.CreateClient(user);

            var codesResponse = await client.GetAsync("Menu/access-codes", cts.Token);
            if (!codesResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Menu access-codes API failed: {(int)codesResponse.StatusCode} {codesResponse.ReasonPhrase}");
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var codes = await codesResponse.Content.ReadFromJsonAsync<List<string>>(cancellationToken: cts.Token)
                ?? new List<string>();

            var versionResponse = await client.GetAsync("Menu/version", cts.Token);
            if (versionResponse.IsSuccessStatusCode)
            {
                var version = await versionResponse.Content.ReadAsStringAsync(cts.Token);
                _sessionState.CachedAccessVersion = version.Trim('"');
            }

            var codeSet = new HashSet<string>(codes.Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code.Trim()), StringComparer.OrdinalIgnoreCase);
            _sessionState.CachedAccessCodes = codeSet;
            _sessionState.CachedAccessRoleId = roleKey;
            return codeSet;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Menu access-codes API error: {ex.Message}");
            var fallback = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _sessionState.CachedAccessCodes = fallback;
            _sessionState.CachedAccessRoleId = roleKey;
            return fallback;
        }
        finally
        {
            _loadingAccessCodes = null;
        }
    }

    private async Task<List<MenuDto>> FetchAccessItemsFromApiAsync(ClaimsPrincipal user, string roleKey)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var client = _apiClient.CreateClient(user);

            // Fetch access items and version in parallel for efficiency.
            var accessItemsTask = client.GetAsync("Menu", cts.Token);
            var versionTask = client.GetAsync("Menu/version", cts.Token);

            await Task.WhenAll(accessItemsTask, versionTask);

            var menuResponse = await accessItemsTask;
            if (!menuResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Menu API failed: {(int)menuResponse.StatusCode} {menuResponse.ReasonPhrase}");
                return new List<MenuDto>();
            }

            var accessItems = await menuResponse.Content.ReadFromJsonAsync<List<MenuDto>>(cancellationToken: cts.Token)
                ?? new List<MenuDto>();

            if (accessItems.Count == 0)
            {
                accessItems = BuildFallbackMenus(user);
            }

            var versionResponse = await versionTask;
            if (versionResponse.IsSuccessStatusCode)
            {
                var version = await versionResponse.Content.ReadAsStringAsync(cts.Token);
                _sessionState.CachedAccessVersion = version.Trim('"');
            }

            _sessionState.CachedAccessItems = accessItems;
            _sessionState.CachedAccessRoleId = roleKey;
            return accessItems;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Menu API error: {ex.Message}");
            var fallbackAccessItems = BuildFallbackMenus(user);
            _sessionState.CachedAccessItems = fallbackAccessItems;
            _sessionState.CachedAccessRoleId = roleKey;
            return fallbackAccessItems;
        }
        finally
        {
            _loadingAccessItems = null;
        }
    }

    private static bool MenuCollectionMatchesRoute(IReadOnlyList<MenuDto> accessItems, string relativePath)
    {
        foreach (var menu in accessItems)
        {
            if (MenuMatchesRoute(menu, relativePath))
            {
                return true;
            }

            foreach (var subMenu in menu.SubMenus)
            {
                if (MenuMatchesRoute(subMenu, relativePath))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MenuMatchesRoute(MenuDto menu, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(menu.MenuUrl))
        {
            return false;
        }

        var cleanMenu = menu.MenuUrl.Split('?')[0].Trim('/');
        var cleanRelative = relativePath.Split('?')[0].Trim('/');

        var menuSegments = cleanMenu.Split('/');
        var relativeSegments = cleanRelative.Split('/');

        if (menuSegments.Length != relativeSegments.Length)
        {
            return false;
        }

        for (int i = 0; i < menuSegments.Length; i++)
        {
            var menuSeg = menuSegments[i];
            var relSeg = relativeSegments[i];

            if (menuSeg.StartsWith('{') && menuSeg.EndsWith('}'))
            {
                continue; // Route parameter wildcard
            }

            if (!menuSeg.Equals(relSeg, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static List<MenuDto> BuildFallbackMenus(ClaimsPrincipal user)
    {
        return new List<MenuDto>();
    }

    private static string GetRoleCacheKey(ClaimsPrincipal user)
    {
        var roleId = user.FindFirst("role_id")?.Value;
        if (!string.IsNullOrWhiteSpace(roleId))
        {
            return $"id:{roleId}";
        }

        var roleName = user.FindFirst(ClaimTypes.Role)?.Value;
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            return $"name:{roleName}";
        }

        return "unknown";
    }
}
