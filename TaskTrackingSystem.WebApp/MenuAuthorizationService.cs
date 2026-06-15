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
        "error"
    };

    private static readonly HashSet<string> MemberRouteSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty,
        "dashboard",
        "home",
        "projects",
        "tasks"
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

        var role = user.FindFirst(ClaimTypes.Role)?.Value ?? "Member";

        if (_sessionState.CachedMenuRole == role && _sessionState.CachedMenus != null)
        {
            return IsRouteAllowed(_sessionState.CachedMenus, relativePath);
        }

        return IsRouteAllowedByRole(role, relativePath);
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

        var role = user.FindFirst(ClaimTypes.Role)?.Value ?? "Member";

        if (_sessionState.CachedMenuRole == role && _sessionState.CachedMenus != null)
        {
            return Task.FromResult(_sessionState.CachedMenus);
        }

        return LoadMenusAsync(user, role);
    }

    public void PreloadMenus(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var role = user.FindFirst(ClaimTypes.Role)?.Value ?? "Member";
        if (_sessionState.CachedMenuRole == role && _sessionState.CachedMenus != null)
        {
            return;
        }

        _ = LoadMenusAsync(user, role);
    }

    private Task<List<MenuDto>> LoadMenusAsync(ClaimsPrincipal user, string role)
    {
        if (_loadingMenus is { IsCompleted: false })
        {
            return _loadingMenus;
        }

        _loadingMenus = FetchMenusFromApiAsync(user, role);
        return _loadingMenus;
    }

    private async Task<List<MenuDto>> FetchMenusFromApiAsync(ClaimsPrincipal user, string role)
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

            _sessionState.CachedMenus = menus;
            _sessionState.CachedMenuRole = role;
            return menus;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Menu API error: {ex.Message}");
            return new List<MenuDto>();
        }
        finally
        {
            _loadingMenus = null;
        }
    }

    private static bool IsRouteAllowedByRole(string role, string relativePath)
    {
        var segment = GetFirstSegment(relativePath);

        if (PublicRouteSegments.Contains(segment))
        {
            return true;
        }

        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (role.Equals("Member", StringComparison.OrdinalIgnoreCase))
        {
            return MemberRouteSegments.Contains(segment);
        }

        return MemberRouteSegments.Contains(segment);
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
}
