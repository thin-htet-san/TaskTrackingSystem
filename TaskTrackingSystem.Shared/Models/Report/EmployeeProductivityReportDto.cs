namespace TaskTrackingSystem.Shared.Models.Report;

public class EmployeeProductivityReportDto
{
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int AssignedCount { get; set; }
    public int CompletedCount { get; set; }
    public int AssignedIssues { get; set; }
    public int OpenIssues { get; set; }
    public int OverdueIssues { get; set; }
    public decimal ActualHours { get; set; }
    public string WorkloadLevel { get; set; } = "Balanced";
    public double AverageCompletionTimeDays { get; set; }
    public string Trend { get; set; } = "Stable";
    public double CompletionRate { get; set; }
    public double Efficiency { get; set; }
    public double OnTimeDeliveryRate { get; set; }
}
