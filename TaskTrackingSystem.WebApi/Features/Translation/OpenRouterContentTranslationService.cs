using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaskTrackingSystem.Shared.Localization;

namespace TaskTrackingSystem.WebApi.Features.Translation;

/// <summary>
/// Server-only OpenRouter Chat Completions provider. Credentials never leave the API process.
/// </summary>
public sealed class OpenRouterContentTranslationService : IContentTranslationService
{
    private const string Provider = "OpenRouter";
    private const string DefaultModel = "openrouter/free";
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConfiguration configuration;
    private readonly ILogger<OpenRouterContentTranslationService> logger;

    public OpenRouterContentTranslationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenRouterContentTranslationService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.configuration = configuration;
        this.logger = logger;
    }

    public Task<TranslationResult> TranslateAsync(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default) =>
        GenerateAsync(sourceText, sourceLanguage, targetLanguage, isName: false, cancellationToken);

    public Task<TranslationResult> TransliterateNameAsync(
        string sourceName,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default) =>
        GenerateAsync(sourceName, sourceLanguage, targetLanguage, isName: true, cancellationToken);

    private async Task<TranslationResult> GenerateAsync(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        bool isName,
        CancellationToken cancellationToken)
    {
        var endpoint = configuration["Translation:Endpoint"];
        var apiKey = configuration["Translation:ApiKey"];
        var model = configuration["Translation:Model"] ?? DefaultModel;
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            return NotConfigured();
        }

        var systemPrompt = isName
            ? "You are a professional name transliterator. Return ONLY the transliterated name."
            : "You are a professional translator. Return ONLY the translated text.";
        var userPrompt = isName
            ? $"Transliterate the personal name by pronunciation from {sourceLanguage} to {targetLanguage}: {sourceText}"
            : $"Translate from {sourceLanguage} to {targetLanguage}: {sourceText}";
        var requestBody = new OpenRouterChatCompletionRequest
        {
            Model = model,
            Messages =
            [
                new OpenRouterMessage { Role = "system", Content = systemPrompt },
                new OpenRouterMessage { Role = "user", Content = userPrompt }
            ],
            Temperature = 0.2
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://huggingface.co");
            request.Headers.TryAddWithoutValidation("X-Title", "TaskTrackingSystem");
            request.Content = JsonContent.Create(requestBody, options: OpenRouterJson.Options);

            var client = httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return FailureForHttpStatus(response.StatusCode, responseBody);
            }

            OpenRouterChatCompletionResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<OpenRouterChatCompletionResponse>(responseBody, OpenRouterJson.Options);
            }
            catch (JsonException)
            {
                return Failure("OpenRouter returned invalid JSON.");
            }

            var translatedText = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(translatedText))
            {
                return Failure("OpenRouter returned empty translated text.");
            }

            var cleanedText = CleanTranslatedText(translatedText);
            if (string.IsNullOrWhiteSpace(cleanedText))
            {
                return Failure("OpenRouter returned empty translated text.");
            }

            if (string.Equals(cleanedText, userPrompt, StringComparison.Ordinal) ||
                string.Equals(cleanedText, systemPrompt, StringComparison.Ordinal))
            {
                return Failure("OpenRouter returned the prompt instead of a translation.");
            }

            return TranslationResult.Generated(cleanedText, Provider);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return Failure("OpenRouter translation request timed out.");
        }
        catch (HttpRequestException)
        {
            return Failure("OpenRouter translation request failed.");
        }
        catch (JsonException)
        {
            return Failure("OpenRouter returned invalid JSON.");
        }
        catch (Exception ex)
        {
            logger.LogWarning("OpenRouter translation request failed with {ExceptionType}.", ex.GetType().Name);
            return Failure("OpenRouter translation request failed.");
        }
    }

    private TranslationResult FailureForHttpStatus(HttpStatusCode statusCode, string responseBody)
    {
        var providerMessage = TryReadErrorMessage(responseBody);
        var message = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"OpenRouter rejected the API key (HTTP {(int)statusCode}).",
            (HttpStatusCode)429 => $"OpenRouter rate limit exceeded (HTTP {(int)statusCode}). Please try again later.",
            _ => $"OpenRouter returned HTTP {(int)statusCode}."
        };

        if (!string.IsNullOrWhiteSpace(providerMessage))
        {
            message = $"{message} {providerMessage}";
        }

        logger.LogWarning("OpenRouter returned HTTP {StatusCode}.", (int)statusCode);
        return Failure(message);
    }

    private static string? TryReadErrorMessage(string responseBody)
    {
        try
        {
            var error = JsonSerializer.Deserialize<OpenRouterErrorEnvelope>(responseBody, OpenRouterJson.Options)?.Error;
            return string.IsNullOrWhiteSpace(error?.Message) ? null : error.Message.Trim();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string CleanTranslatedText(string text)
    {
        var cleaned = text.Trim();
        if (cleaned.StartsWith("```") && cleaned.EndsWith("```"))
        {
            cleaned = cleaned[3..^3].Trim();
        }

        while (cleaned.Length >= 2 &&
               ((cleaned[0] == '"' && cleaned[^1] == '"') ||
                (cleaned[0] == '“' && cleaned[^1] == '”') ||
                (cleaned[0] == '\'' && cleaned[^1] == '\'')))
        {
            cleaned = cleaned[1..^1].Trim();
        }

        return cleaned;
    }

    private static TranslationResult NotConfigured() =>
        new(false, null, "Translation provider is not configured.", Provider, false);

    private static TranslationResult Failure(string message) =>
        new(false, null, message, Provider, false);
}

internal static class OpenRouterJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
}

internal sealed class OpenRouterChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OpenRouterMessage> Messages { get; set; } = [];

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }
}

internal sealed class OpenRouterMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

internal sealed class OpenRouterChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<OpenRouterChoice>? Choices { get; set; }
}

internal sealed class OpenRouterChoice
{
    [JsonPropertyName("message")]
    public OpenRouterMessage? Message { get; set; }
}

internal sealed class OpenRouterErrorEnvelope
{
    [JsonPropertyName("error")]
    public OpenRouterError? Error { get; set; }
}

internal sealed class OpenRouterError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
