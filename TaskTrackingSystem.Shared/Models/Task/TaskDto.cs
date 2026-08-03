using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaskTrackingSystem.Shared.Enums;
using AppTaskStatus = TaskTrackingSystem.Shared.Enums.AppTaskStatus;

namespace TaskTrackingSystem.Shared.Models.Task
{
    public class TaskDto
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? TitleMy { get; set; }
        public string? Description { get; set; }
        public string? DescriptionMy { get; set; }
        public long ProjectId { get; set; }
        public AppTaskStatus StatusId { get; set; }
        public TaskPriority PriorityId { get; set; }
        public long? AssignedTo { get; set; }
        public long? AssignedBy { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsArchived { get; set; }
    }
}
