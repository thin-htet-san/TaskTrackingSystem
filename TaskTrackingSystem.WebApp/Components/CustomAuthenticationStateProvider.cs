using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace TaskTrackingSystem.WebApp.Components;

// Cookie auth with in-circuit cache so interactive Blazor pages keep the signed-in user.
public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserSessionState _sessionState;
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public CustomAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor, UserSessionState sessionState)
    {
        _httpContextAccessor = httpContextAccessor;
        _sessionState = sessionState;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            CacheUser(user);
            return Task.FromResult(new AuthenticationState(user));
        }

        if (_sessionState.CachedUser?.Identity?.IsAuthenticated == true)
        {
            return Task.FromResult(new AuthenticationState(_sessionState.CachedUser));
        }

        return Task.FromResult(Anonymous);
    }

    public void NotifyUserAuthenticationChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private void CacheUser(ClaimsPrincipal user)
    {
        _sessionState.CachedUser = user;

        var token = user.FindFirst("jwt_token")?.Value;
        if (!string.IsNullOrEmpty(token))
        {
            _sessionState.Token = token;
        }
    }
}
