using System.Net.Http.Headers;
using System.Text.Json;
using TaskTrackingSystem.Shared.Localization;

namespace TaskTrackingSystem.WebApi.Features.Translation;

/// <summary>
/// Optional server-side provider adapter. It is inert until Translation:Endpoint is configured.
/// </summary>
public sealed class ConfiguredHttpContentTranslationService : IContentTranslationService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConfiguration configuration;
    private readonly ILogger<ConfiguredHttpContentTranslationService> logger;

    public ConfiguredHttpContentTranslationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ConfiguredHttpContentTranslationService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.configuration = configuration;
        this.logger = logger;
    }

    public Task<TranslationResult> TranslateAsync(string sourceText, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default) =>
        SendAsync(sourceText, sourceLanguage, targetLanguage, false, cancellationToken);

    public Task<TranslationResult> TransliterateNameAsync(string sourceName, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default) =>
        SendAsync(sourceName, sourceLanguage, targetLanguage, true, cancellationToken);

    private async Task<TranslationResult> SendAsync(string sourceText, string sourceLanguage, string targetLanguage, bool isName, CancellationToken cancellationToken)
    {
        var endpoint = configuration["Translation:Endpoint"] ?? Environment.GetEnvironmentVariable("TRANSLATION_API_URL");
        if (string.IsNullOrWhiteSpace(endpoint)) return TranslationResult.NotConfigured();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            var key = configuration["Translation:ApiKey"] ?? Environment.GetEnvironmentVariable("TRANSLATION_API_KEY");
            if (!string.IsNullOrWhiteSpace(key)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Content = JsonContent.Create(new
            {
                text = sourceText,
                sourceLanguage,
                targetLanguage,
                mode = isName ? "transliteration" : "translation",
                instruction = isName
                    ? "Transliterate pronunciation/spelling only. Do not translate semantic meaning. Return the name only."
                    : "Translate to natural formal Burmese or English. Preserve names unless requested, codes, IDs, dates, numbers, placeholders, URLs, and appropriate technical terms. Return translated text only."
            });

            var client = httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Translation provider returned HTTP {StatusCode}.", (int)response.StatusCode);
                return new(false, null, $"Translation provider returned {(int)response.StatusCode}.", ProviderName(), false);
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var text = TryGetString(root, "translatedText") ?? TryGetString(root, "translation") ?? TryGetString(root, "text");
            return string.IsNullOrWhiteSpace(text)
                ? new(false, null, "Translation provider returned no translated text.", ProviderName(), false)
                : TranslationResult.Generated(text.Trim(), ProviderName());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Translation provider request failed: {Message}", ex.Message);
            return new(false, null, "Translation provider request failed.", ProviderName(), false);
        }
    }

    private string ProviderName() => configuration["Translation:Provider"] ?? Environment.GetEnvironmentVariable("TRANSLATION_PROVIDER") ?? "configured-http";

    private static string? TryGetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
