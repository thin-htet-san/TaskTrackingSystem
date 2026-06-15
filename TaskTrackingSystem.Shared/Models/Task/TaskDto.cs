using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskTrackingSystem.Shared.Models.Task
{
    public class TaskDto
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public long ProjectId { get; set; }
        public long StatusId { get; set; }
        public long PriorityId { get; set; }
        public long? AssignedTo { get; set; }
        public long? AssignedBy { get; set; }
        public int? EstimatedHours { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
