namespace TaskTrackingSystem.Shared.Models.Report
{
    public class TeamProductivityReportDto
    {
        public long UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int TotalAssigned { get; set; }
        public int Completed { get; set; }
        public int InProgress { get; set; }
        public int ToDo { get; set; }
        public int Overdue { get; set; }
        public double CompletionRate { get; set; }
    }
}
