using System;

namespace TaskTrackingSystem.Shared.Models.Report
{
    public class TaskStatusSummaryDto
    {
        public long TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? TitleMy { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? ProjectNameMy { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string PriorityName { get; set; } = string.Empty;
        public string? AssignedTo { get; set; }
        public string? AssignedToMy { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsOverdue => DueDate < DateTime.UtcNow && StatusName != "Done";
    }
}
