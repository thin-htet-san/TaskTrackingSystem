namespace TaskTrackingSystem.WebApp.Components.Shared.Charts;

public sealed record ChartDataPoint(string Label, double Value, string Color = "#7c3aed");

public sealed record BarChartGroup(string Label, IReadOnlyList<ChartDataPoint> Bars, string? Detail = null);

public sealed record LineChartPoint(string Label, double Value, string? Detail = null);

public sealed record ProgressListChartItem(
    string Label,
    double Value,
    string ValueLabel,
    string Color = "#7c3aed",
    string? Detail = null,
    string? LeadingText = null);

public sealed record TimelineChartItem(
    string Label,
    DateTime StartDate,
    DateTime EndDate,
    double CompletionPercentage,
    string Color = "#7c3aed",
    string? Detail = null);
