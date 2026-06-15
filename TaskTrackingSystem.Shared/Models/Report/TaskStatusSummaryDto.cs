using System;

namespace TaskTrackingSystem.Shared.Models.Report
{
    public class TaskStatusSummaryDto
    {
        public long TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string PriorityName { get; set; } = string.Empty;
        public string? AssignedTo { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsOverdue => DueDate < DateTime.UtcNow && StatusName != "Done";
    }
}
