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
    private Task<List<MenuDto>>? _loadingMenus;

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

        var roleKey = GetRoleCacheKey(user);

        if (_sessionState.CachedMenuRoleId == roleKey && _sessionState.CachedMenus != null)
        {
            return IsRouteAllowed(_sessionState.CachedMenus, relativePath);
        }

        return false;
    }

    public bool IsRouteAllowed(IReadOnlyList<MenuDto> menus, string relativePath)
    {
        var firstSegment = GetFirstSegment(relativePath);

        if (PublicRouteSegments.Contains(firstSegment))
        {
            return true;
        }

        if (firstSegment.Equals("dashboard", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("home", StringComparison.OrdinalIgnoreCase))
        {
            return MenuCollectionMatchesSegment(menus, "dashboard");
        }

        return MenuCollectionMatchesSegment(menus, firstSegment);
    }

    public Task<List<MenuDto>> GetUserMenusAsync(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(new List<MenuDto>());
        }

        var roleKey = GetRoleCacheKey(user);

        if (_sessionState.CachedMenuRoleId == roleKey && _sessionState.CachedMenus != null)
        {
            return Task.FromResult(_sessionState.CachedMenus);
        }

        return LoadMenusAsync(user, roleKey);
    }

    public void PreloadMenus(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var roleKey = GetRoleCacheKey(user);
        if (_sessionState.CachedMenuRoleId == roleKey && _sessionState.CachedMenus != null)
        {
            return;
        }

        _ = LoadMenusAsync(user, roleKey);
    }

    private Task<List<MenuDto>> LoadMenusAsync(ClaimsPrincipal user, string roleKey)
    {
        if (_loadingMenus is { IsCompleted: false })
        {
            return _loadingMenus;
        }

        _loadingMenus = FetchMenusFromApiAsync(user, roleKey);
        return _loadingMenus;
    }

    private async Task<List<MenuDto>> FetchMenusFromApiAsync(ClaimsPrincipal user, string roleKey)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var client = _apiClient.CreateClient(user);
            var response = await client.GetAsync("Menu", cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Menu API failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                return new List<MenuDto>();
            }

            var menus = await response.Content.ReadFromJsonAsync<List<MenuDto>>(cancellationToken: cts.Token)
                ?? new List<MenuDto>();

            if (menus.Count == 0)
            {
                menus = BuildFallbackMenus(user);
            }

            _sessionState.CachedMenus = menus;
            _sessionState.CachedMenuRoleId = roleKey;
            return menus;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Menu API error: {ex.Message}");
            var fallbackMenus = BuildFallbackMenus(user);
            _sessionState.CachedMenus = fallbackMenus;
            _sessionState.CachedMenuRoleId = roleKey;
            return fallbackMenus;
        }
        finally
        {
            _loadingMenus = null;
        }
    }

    private static bool MenuCollectionMatchesSegment(IReadOnlyList<MenuDto> menus, string segment)
    {
        foreach (var menu in menus)
        {
            if (MenuMatchesSegment(menu, segment))
            {
                return true;
            }

            foreach (var subMenu in menu.SubMenus)
            {
                if (MenuMatchesSegment(subMenu, segment))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MenuMatchesSegment(MenuDto menu, string segment)
    {
        if (string.IsNullOrWhiteSpace(menu.MenuUrl))
        {
            return false;
        }

        var menuSegment = menu.MenuUrl.Split('?')[0].Trim('/').Split('/')[0];
        return menuSegment.Equals(segment, StringComparison.OrdinalIgnoreCase);
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

        return string.Empty;
    }

    private static List<MenuDto> BuildFallbackMenus(ClaimsPrincipal user)
    {
        var roleName = user.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var menus = new List<MenuDto>
        {
            CreateMenu("dashboard", "Dashboard", "/dashboard", 1, "layout-dashboard")
        };

        if (roleName.Equals("Employee", StringComparison.OrdinalIgnoreCase))
        {
            menus.Add(CreateMenu("tasks", "Tasks", "/tasks", 2, "list-checks"));
            return menus;
        }

        menus.Add(CreateMenu("tasks", "Tasks", "/tasks", 2, "list-checks"));
        menus.Add(CreateMenu("projects", "Projects", "/projects", 3, "folder-kanban"));
        menus.Add(CreateMenu("reports", "Reports", "/reports/tasks", 4, "bar-chart-3"));

        if (roleName.Equals("Manager", StringComparison.OrdinalIgnoreCase) ||
            roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            if (roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                menus.Add(CreateMenu("users", "Users", "/users", 5, "users"));
                menus.Add(CreateMenu("roles", "Roles", "/roles", 6, "shield"));
            }
        }

        return menus;
    }

    private static MenuDto CreateMenu(string code, string name, string url, int orderNo, string icon)
    {
        return new MenuDto
        {
            MenuCode = code,
            MenuName = name,
            MenuUrl = url,
            OrderNo = orderNo,
            Icon = icon
        };
    }
}
