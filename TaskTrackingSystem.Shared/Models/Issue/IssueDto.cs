using System;
using TaskTrackingSystem.Shared.Enums;

namespace TaskTrackingSystem.Shared.Models.Issue
{
    public class IssueDto
    {
        public long Id { get; set; }
        public long TaskId { get; set; }
        public string TaskTitle { get; set; } = string.Empty;
        public long ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public long? AssignedTo { get; set; }
        public string? AssignedToName { get; set; }
        public decimal? EstimatedHours { get; set; }
        public decimal? ActualHours { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime DueDate { get; set; }
        public AppTaskStatus StatusId { get; set; }
        public TaskPriority PriorityId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
