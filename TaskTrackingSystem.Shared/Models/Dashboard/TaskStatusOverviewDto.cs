namespace TaskTrackingSystem.Shared.Models.Dashboard
{
    public class TaskStatusOverviewDto
    {
        public string StatusName { get; set; } = string.Empty;
        public long StatusId { get; set; }
        public int TaskCount { get; set; }
    }
}
