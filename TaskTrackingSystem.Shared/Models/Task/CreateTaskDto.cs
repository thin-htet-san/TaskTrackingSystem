using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskTrackingSystem.Shared.Models.Task
{
    public class CreateTaskDto
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public long ProjectId { get; set; }

        public long StatusId { get; set; }

        public long PriorityId { get; set; }

        public long? AssignedTo { get; set; }

        public long? AssignedBy { get; set; }

        public int? EstimatedHours { get; set; }

        [Required]
        public DateTime DueDate { get; set; }
    }
}
