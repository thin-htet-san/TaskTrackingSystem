namespace TaskTrackingSystem.Shared.Models.Report;

public class IssueReportSummaryDto
{
    public int TotalIssues { get; set; }
    public int OpenIssues { get; set; }
    public int OverdueIssues { get; set; }
    public int BlockedIssues { get; set; }
    public decimal EstimatedHours { get; set; }
    public decimal ActualHours { get; set; }
    public decimal VarianceHours { get; set; }
    public double UtilizationPercent { get; set; }
}
