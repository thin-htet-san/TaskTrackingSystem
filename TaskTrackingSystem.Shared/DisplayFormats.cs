using System.Globalization;

namespace TaskTrackingSystem.Shared;

public static class DisplayFormats
{
    public const string DateFormat = "dd-MM-yyyy";

    public static string Date(DateTime value) =>
        value.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string Date(DateTime? value) =>
        value.HasValue ? Date(value.Value) : string.Empty;

    public static string Date(DateOnly value) =>
        value.ToString(DateFormat, CultureInfo.InvariantCulture);
}
