using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TaskTrackingSystem.Shared.Localization;
using TaskTrackingSystem.WebApi.Features.Translation;
using Xunit;

namespace TaskTrackingSystem.Tests;

public sealed class OpenRouterContentTranslationServiceTests
{
    private const string Endpoint = "https://openrouter.ai/api/v1/chat/completions";

    [Fact]
    public async Task TranslateAsync_SendsOpenRouterRequestAndParsesText()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"choices":[{"message":{"content":"  \"Hello\"  "}}]}""");
        var result = await CreateService(handler, "secret-test-key").TranslateAsync("မင်္ဂလာပါ", "Burmese", "English");

        Assert.True(result.Success);
        Assert.Equal("Hello", result.TranslatedText);
        Assert.Equal("OpenRouter", result.Provider);
        Assert.Equal("Bearer secret-test-key", handler.Request!.Headers.Authorization?.ToString());
        Assert.Equal("https://huggingface.co", handler.Request.Headers.GetValues("HTTP-Referer").Single());
        Assert.Equal("TaskTrackingSystem", handler.Request.Headers.GetValues("X-Title").Single());

        using var body = JsonDocument.Parse(handler.RequestBody!);
        var root = body.RootElement;
        Assert.Equal("openrouter/free", root.GetProperty("model").GetString());
        Assert.Equal(0.2, root.GetProperty("temperature").GetDouble());
        Assert.Equal("system", root.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("You are a professional translator. Return ONLY the translated text.", root.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Contains("Translate from Burmese to English: မင်္ဂလာပါ", root.GetProperty("messages")[1].GetProperty("content").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransliterateNameAsync_UsesNamePrompt()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"choices":[{"message":{"content":"John Smith"}}]}""");
        var result = await CreateService(handler, "key").TransliterateNameAsync("ဂျွန် စမစ်", "Burmese", "English");

        Assert.True(result.Success);
        Assert.Equal("John Smith", result.TranslatedText);
        using var body = JsonDocument.Parse(handler.RequestBody!);
        var systemText = body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();
        var userText = body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString();
        Assert.Contains("name transliterator", systemText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Transliterate the personal name", userText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingEndpointOrKey_ReturnsControlledFailure()
    {
        var endpointMissing = CreateService(new StubHandler(HttpStatusCode.OK, "{}"), "key", endpoint: "");
        var keyMissing = CreateService(new StubHandler(HttpStatusCode.OK, "{}"), "");

        var endpointResult = await endpointMissing.TranslateAsync("Project", "English", "Burmese");
        var keyResult = await keyMissing.TranslateAsync("Project", "English", "Burmese");

        Assert.False(endpointResult.Success);
        Assert.False(keyResult.Success);
        Assert.Equal("Translation provider is not configured.", endpointResult.ErrorMessage);
        Assert.Equal("OpenRouter", endpointResult.Provider);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task HttpErrors_ReturnControlledFailure(int statusCode)
    {
        var handler = new StubHandler((HttpStatusCode)statusCode, """{"error":{"message":"provider detail"}}""");
        var result = await CreateService(handler, "secret-test-key").TranslateAsync("Project", "English", "Burmese");

        Assert.False(result.Success);
        Assert.Equal("OpenRouter", result.Provider);
        Assert.Null(result.TranslatedText);
        Assert.Contains(statusCode.ToString(), result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-test-key", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[{\"message\":{}}]}")]
    [InlineData("not-json")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"   \"}}]}")]
    public async Task EmptyOrInvalidResponses_ReturnControlledFailure(string responseBody)
    {
        var handler = new StubHandler(HttpStatusCode.OK, responseBody);
        var result = await CreateService(handler, "key").TranslateAsync("Project", "English", "Burmese");

        Assert.False(result.Success);
        Assert.Equal("OpenRouter", result.Provider);
        Assert.Null(result.TranslatedText);
    }

    [Fact]
    public async Task Cancellation_IsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = CreateService(new StubHandler(HttpStatusCode.OK, "{}"), "key");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.TranslateAsync("Project", "English", "Burmese", cancellation.Token));
    }

    private static OpenRouterContentTranslationService CreateService(StubHandler handler, string apiKey, string endpoint = Endpoint)
    {
        var values = new Dictionary<string, string?>
        {
            ["Translation:Endpoint"] = endpoint,
            ["Translation:ApiKey"] = apiKey,
            ["Translation:Model"] = "openrouter/free"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new OpenRouterContentTranslationService(
            new StubHttpClientFactory(handler),
            configuration,
            NullLogger<OpenRouterContentTranslationService>.Instance);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient client;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            client = new HttpClient(handler);
        }

        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly string responseBody;

        public StubHandler(HttpStatusCode statusCode, string responseBody)
        {
            this.statusCode = statusCode;
            this.responseBody = responseBody;
        }

        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
