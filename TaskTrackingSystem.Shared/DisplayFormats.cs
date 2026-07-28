using System.Globalization;
using TaskTrackingSystem.Shared.Localization;

namespace TaskTrackingSystem.Shared;

public static class DisplayFormats
{
    public const string DateFormat = "dd-MM-yyyy";

    public static string Date(DateTime value) =>
        AppLocalization.LocalizeDigits(value.ToString(DateFormat, CultureInfo.InvariantCulture));

    public static string Date(DateTime? value) =>
        value.HasValue ? Date(value.Value) : string.Empty;

    public static string Date(DateOnly value) =>
        AppLocalization.LocalizeDigits(value.ToString(DateFormat, CultureInfo.InvariantCulture));

    public static string Number(int value) =>
        AppLocalization.LocalizeDigits(value.ToString(CultureInfo.InvariantCulture));

    public static string Number(long value) =>
        AppLocalization.LocalizeDigits(value.ToString(CultureInfo.InvariantCulture));

    public static string Number(decimal value, string format = "0.#") =>
        AppLocalization.LocalizeDigits(value.ToString(format, CultureInfo.InvariantCulture));

    public static string Number(double value, string format = "0.#") =>
        AppLocalization.LocalizeDigits(value.ToString(format, CultureInfo.InvariantCulture));
}
