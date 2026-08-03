using System.Net.Http.Json;
using TaskTrackingSystem.Shared.Localization;

namespace TaskTrackingSystem.WebApp.Localization;

/// <summary>
/// Client-side proxy only. Provider credentials stay inside the Web API.
/// </summary>
public sealed class ApiContentTranslationService : IContentTranslationService
{
    private readonly ApiClientService apiClient;

    public ApiContentTranslationService(ApiClientService apiClient)
    {
        this.apiClient = apiClient;
    }

    public Task<TranslationResult> TranslateAsync(string sourceText, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default) =>
        GenerateAsync(new TranslationRequest(sourceText, sourceLanguage, targetLanguage), cancellationToken);

    public Task<TranslationResult> TransliterateNameAsync(string sourceName, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default) =>
        GenerateAsync(new TranslationRequest(sourceName, sourceLanguage, targetLanguage, true), cancellationToken);

    private async Task<TranslationResult> GenerateAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await apiClient.CreateClient().PostAsJsonAsync("Translation/generate", request, cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<TranslationResult>(cancellationToken: cancellationToken);
            return result ?? new TranslationResult(false, null, $"Translation request failed ({(int)response.StatusCode}).", "api", false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new TranslationResult(false, null, ex.Message, "api", false);
        }
    }
}
