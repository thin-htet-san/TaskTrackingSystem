namespace TaskTrackingSystem.Shared.Models.Report;

public class ProjectProgressReportDto
{
    public long ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public double Progress { get; set; }
    public bool IsAhead { get; set; }
    public bool IsAtRisk { get; set; }
}
