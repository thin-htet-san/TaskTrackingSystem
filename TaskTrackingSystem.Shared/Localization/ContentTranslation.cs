namespace TaskTrackingSystem.Shared.Localization;

public sealed record TranslationResult(
    bool Success,
    string? TranslatedText,
    string? ErrorMessage,
    string? Provider,
    bool WasGenerated)
{
    public static TranslationResult NotConfigured() =>
        new(false, null, "Translation provider is not configured.", "none", false);

    public bool Succeeded => Success;
    public string? Text => TranslatedText;

    public static TranslationResult Generated(string text, string provider) =>
        new(true, text, null, provider, true);
}

public interface IContentTranslationService
{
    Task<TranslationResult> TranslateAsync(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default);

    Task<TranslationResult> TransliterateNameAsync(
        string sourceName,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default);
}

public sealed record TranslationRequest(
    string Text,
    string SourceLanguage,
    string TargetLanguage,
    bool IsName = false);

public sealed class NoOpContentTranslationService : IContentTranslationService
{
    public Task<TranslationResult> TranslateAsync(string sourceText, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default) =>
        Task.FromResult(TranslationResult.NotConfigured());

    public Task<TranslationResult> TransliterateNameAsync(string sourceName, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default) =>
        Task.FromResult(TranslationResult.NotConfigured());
}
