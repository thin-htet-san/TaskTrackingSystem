using System;

namespace TaskTrackingSystem.Shared.Models.Report
{
    public class TaskReportDto
    {
        public long TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public long ProjectId { get; set; }
        public long StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public long PriorityId { get; set; }
        public string PriorityName { get; set; } = string.Empty;
        public string? AssignedToUser { get; set; }
        public long? AssignedToUserId { get; set; }
        public string? AssignedByUser { get; set; }
        public int? EstimatedHours { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
