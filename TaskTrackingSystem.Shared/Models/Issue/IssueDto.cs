using System;
using TaskTrackingSystem.Shared.Enums;

namespace TaskTrackingSystem.Shared.Models.Issue
{
    public class IssueDto
    {
        public long Id { get; set; }
        public long TaskId { get; set; }
        public string TaskTitle { get; set; } = string.Empty;
        public string? TaskTitleMy { get; set; }
        public long ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? ProjectNameMy { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? TitleMy { get; set; }
        public string? Description { get; set; }
        public string? DescriptionMy { get; set; }
        public long? AssignedTo { get; set; }
        public string? AssignedToName { get; set; }
        public string? AssignedToNameMy { get; set; }
        public decimal? EstimatedHours { get; set; }
        public decimal? ActualHours { get; set; }
        public string? DelayReason { get; set; }
        public string? DelayReasonMy { get; set; }
        public bool IsBlocked { get; set; }
        public string? BlockedBy { get; set; }
        public string? BlockedByMy { get; set; }
        public int EscalationLevel { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime DueDate { get; set; }
        public AppTaskStatus StatusId { get; set; }
        public TaskPriority PriorityId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
