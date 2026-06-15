using System;
using System.Net.Http;

namespace TaskTrackingSystem.WebApp;

public class AuthorizedHttpClientFactory : IHttpClientFactory
{
    private readonly IHttpClientFactory _innerFactory;
    private readonly UserSessionState _sessionState;

    public AuthorizedHttpClientFactory(IHttpClientFactory innerFactory, UserSessionState sessionState)
    {
        _innerFactory = innerFactory;
        _sessionState = sessionState;
    }

    public HttpClient CreateClient(string name)
    {
        var client = _innerFactory.CreateClient(name);
        if (name == "WebApi")
        {
            try
            {
                var token = _sessionState.Token;
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthorizedHttpClientFactory] Error attaching bearer token: {ex.Message}");
            }
        }
        return client;
    }
}
