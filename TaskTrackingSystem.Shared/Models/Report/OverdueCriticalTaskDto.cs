using System;

namespace TaskTrackingSystem.Shared.Models.Report
{
    public class OverdueCriticalTaskDto
    {
        public long TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? TitleMy { get; set; }
        public string? Description { get; set; }
        public string? DescriptionMy { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? ProjectNameMy { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string PriorityName { get; set; } = string.Empty;
        public string? AssignedTo { get; set; }
        public string? AssignedToMy { get; set; }
        public long? AssignedToUserId { get; set; }
        public DateTime DueDate { get; set; }
        public int DaysOverdue { get; set; }
        public DateTime CreatedAt { get; set; }
        public int OverdueIssues { get; set; }
        public string DelayReason { get; set; } = string.Empty;
        public string? DelayReasonMy { get; set; }
        public string BlockedBy { get; set; } = string.Empty;
        public string? BlockedByMy { get; set; }
        public int EscalationLevel { get; set; }
        public string RecordType { get; set; } = "Task";
    }
}
