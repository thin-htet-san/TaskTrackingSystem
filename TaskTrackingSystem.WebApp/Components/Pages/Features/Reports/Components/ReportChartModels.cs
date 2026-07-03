namespace TaskTrackingSystem.WebApp.Components.Pages.Features.Reports.Components;

public sealed record ReportChartItem(string Label, double Value, string Color = "#7c3aed", string? Detail = null);

public sealed record StackedSegment(string Label, double Value, string Color, string? Detail = null);

public sealed record HeatmapCell(string Row, string Column, double Value, string? Detail = null);

public sealed record RiskMatrixPoint(string Label, int Severity, int Urgency, double Value, string Color = "#7c3aed", string? Detail = null);

public sealed record TopRiskItem(string Title, string Meta, double Score, string Badge, string Tone = "slate", string? Detail = null);

public sealed record SparklinePoint(string Label, double Value);
