using TaskTrackingSystem.Shared.Enums;
using AppTaskStatus = TaskTrackingSystem.Shared.Enums.AppTaskStatus;

namespace TaskTrackingSystem.Shared.Models.Dashboard
{
    public class TaskStatusOverviewDto
    {
        public string StatusName { get; set; } = string.Empty;
        public AppTaskStatus StatusId { get; set; }
        public int TaskCount { get; set; }
    }
}
