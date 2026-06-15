namespace TaskTrackingSystem.Shared.Models.Dashboard
{
    public class DashboardSummaryDto
    {
        public int TotalUsers { get; set; }
        public int ActiveProjectsCount { get; set; }
        public int PendingTasksCount { get; set; }
    }
}
