using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace TaskTrackingSystem.WebApp;

/// <summary>
/// Scoped service that creates authorized HttpClient instances for the WebApi.
/// Inject this instead of IHttpClientFactory in Blazor components.
/// </summary>
public class ApiClientService
{
    private readonly IHttpClientFactory _factory;
    private readonly UserSessionState _sessionState;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>The base URL of the WebApi (without trailing /api/). Used for direct file download links.</summary>
    public string BaseUrl { get; }

    public ApiClientService(
        IHttpClientFactory factory,
        UserSessionState sessionState,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
    {
        _factory = factory;
        _sessionState = sessionState;
        _httpContextAccessor = httpContextAccessor;

        // Strip trailing /api/ so components can build their own paths
        var rawUrl = configuration["WebApi:BaseUrl"] ?? "http://127.0.0.1:5018/api/";
        BaseUrl = rawUrl.TrimEnd('/').EndsWith("/api", StringComparison.OrdinalIgnoreCase)
            ? rawUrl.TrimEnd('/')[..^4]   // remove trailing /api
            : rawUrl.TrimEnd('/');
    }

    public HttpClient CreateClient()
    {
        return CreateClient(null);
    }

    public HttpClient CreateClient(ClaimsPrincipal? user)
    {
        var client = _factory.CreateClient("WebApi");
        var token = ResolveToken(user);

        if (!string.IsNullOrEmpty(token))
        {
            _sessionState.Token = token;
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    public string? GetCurrentToken(ClaimsPrincipal? user = null) => ResolveToken(user);

    private string? ResolveToken(ClaimsPrincipal? user)
    {
        if (!string.IsNullOrEmpty(_sessionState.Token))
        {
            return _sessionState.Token;
        }

        var claimToken = user?.FindFirst("jwt_token")?.Value;
        if (!string.IsNullOrEmpty(claimToken))
        {
            return claimToken;
        }

        return _httpContextAccessor.HttpContext?.User.FindFirst("jwt_token")?.Value;
    }
}
