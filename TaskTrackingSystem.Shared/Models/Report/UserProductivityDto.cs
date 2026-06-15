namespace TaskTrackingSystem.Shared.Models.Report
{
    public class UserProductivityDto
    {
        public long UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int TotalAssignedTasks { get; set; }
        public int CompletedTasksCount { get; set; }
        public double EfficiencyRatio { get; set; } // Completed / Total Asssined (percentage or ratio)
    }
}
