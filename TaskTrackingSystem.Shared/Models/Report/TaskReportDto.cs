using System;
using TaskTrackingSystem.Shared.Enums;
using AppTaskStatus = TaskTrackingSystem.Shared.Enums.AppTaskStatus;

namespace TaskTrackingSystem.Shared.Models.Report
{
    public class TaskReportDto
    {
        public long TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? TitleMy { get; set; }
        public string? Description { get; set; }
        public string? DescriptionMy { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? ProjectNameMy { get; set; }
        public long ProjectId { get; set; }
        public AppTaskStatus StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public TaskPriority PriorityId { get; set; }
        public string PriorityName { get; set; } = string.Empty;
        public string? AssignedToUser { get; set; }
        public string? AssignedToUserMy { get; set; }
        public long? AssignedToUserId { get; set; }
        public string? AssignedByUser { get; set; }
        public string? AssignedByUserMy { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public int OpenIssues { get; set; }
        public int OverdueIssues { get; set; }
        public int BlockedIssues { get; set; }
        public string IssueSummary { get; set; } = string.Empty;
        public string TaskHealth { get; set; } = "Healthy";
    }
}
