namespace TaskTrackingSystem.Shared.Models.Report;

public class EmployeeProductivityReportDto
{
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int AssignedCount { get; set; }
    public int CompletedCount { get; set; }
    public double Efficiency { get; set; }
    public double OnTimeDeliveryRate { get; set; }
}
