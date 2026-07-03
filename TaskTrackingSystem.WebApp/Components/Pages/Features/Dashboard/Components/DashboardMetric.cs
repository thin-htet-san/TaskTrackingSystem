namespace TaskTrackingSystem.WebApp.Components.Pages.Features.Dashboard.Components;

public sealed record DashboardMetric(
    string Icon,
    string BadgeText,
    string Value,
    string Label,
    string IconWrapClass,
    string BadgeClass);
