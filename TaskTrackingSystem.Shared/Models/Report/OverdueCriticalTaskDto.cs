using System;

namespace TaskTrackingSystem.Shared.Models.Report
{
    public class OverdueCriticalTaskDto
    {
        public long TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string PriorityName { get; set; } = string.Empty;
        public string? AssignedTo { get; set; }
        public DateTime DueDate { get; set; }
        public int DaysOverdue { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
