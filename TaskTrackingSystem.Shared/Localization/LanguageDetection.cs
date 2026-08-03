using System.Text;

namespace TaskTrackingSystem.Shared.Localization;

public enum DetectedContentLanguage
{
    Empty,
    Burmese,
    English,
    Mixed,
    Unknown
}

public sealed record LanguageDetectionResult(
    DetectedContentLanguage Language,
    int BurmeseCharacters,
    int LatinCharacters,
    int OtherCharacters,
    double BurmeseRatio,
    double EnglishRatio);

/// <summary>
/// Local, advisory detection used only to help users move content between fields.
/// It deliberately does not reject mixed-language business text.
/// </summary>
public sealed class LanguageDetectionService
{
    public LanguageDetectionResult Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new(DetectedContentLanguage.Empty, 0, 0, 0, 0, 0);
        }

        var burmese = 0;
        var latin = 0;
        var other = 0;

        foreach (var rune in text.EnumerateRunes())
        {
            if (IsBurmese(rune.Value))
            {
                burmese++;
            }
            else if ((rune.Value >= 'A' && rune.Value <= 'Z') || (rune.Value >= 'a' && rune.Value <= 'z'))
            {
                latin++;
            }
            else if (!Rune.IsWhiteSpace(rune) && !Rune.IsPunctuation(rune) && !Rune.IsNumber(rune) && !Rune.IsSymbol(rune))
            {
                other++;
            }
        }

        var letters = burmese + latin + other;
        if (letters == 0)
        {
            return new(DetectedContentLanguage.Unknown, burmese, latin, other, 0, 0);
        }

        var burmeseRatio = burmese / (double)letters;
        var englishRatio = latin / (double)letters;
        var language = burmeseRatio >= 0.70 && burmese > 0
            ? DetectedContentLanguage.Burmese
            : englishRatio >= 0.70 && latin > 0
                ? DetectedContentLanguage.English
                : burmese > 0 && latin > 0
                    ? DetectedContentLanguage.Mixed
                    : DetectedContentLanguage.Unknown;

        return new(language, burmese, latin, other, burmeseRatio, englishRatio);
    }

    public bool AppearsPredominantlyBurmese(string? text) => Detect(text).Language == DetectedContentLanguage.Burmese;
    public bool AppearsPredominantlyEnglish(string? text) => Detect(text).Language == DetectedContentLanguage.English;

    private static bool IsBurmese(int value) =>
        (value >= 0x1000 && value <= 0x109F) ||
        (value >= 0xAA60 && value <= 0xAA7F) ||
        (value >= 0xA9E0 && value <= 0xA9FF);
}
