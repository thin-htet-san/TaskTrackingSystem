namespace TaskTrackingSystem.Shared.Models.Report;

public class ProjectProgressReportDto
{
    public long ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OpenIssues { get; set; }
    public int OverdueIssues { get; set; }
    public int BlockedIssues { get; set; }
    public double Progress { get; set; }
    public bool IsAhead { get; set; }
    public bool IsAtRisk { get; set; }
    public string ProjectHealth { get; set; } = "Healthy";
    public DateTime LastActivity { get; set; }
    public DateTime? NextMilestone { get; set; }
    public decimal ActualHours { get; set; }
}
