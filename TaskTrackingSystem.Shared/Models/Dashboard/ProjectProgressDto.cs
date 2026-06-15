namespace TaskTrackingSystem.Shared.Models.Dashboard
{
    public class ProjectProgressDto
    {
        public long ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public double CompletionPercentage { get; set; }
        public int CompletedTasksCount { get; set; }
        public int TotalTasksCount { get; set; }
    }
}
