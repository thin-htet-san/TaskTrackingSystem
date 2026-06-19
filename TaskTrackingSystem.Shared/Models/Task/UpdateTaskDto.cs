using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TaskTrackingSystem.Shared.Enums;
using AppTaskStatus = TaskTrackingSystem.Shared.Enums.AppTaskStatus;

namespace TaskTrackingSystem.Shared.Models.Task
{
    public class UpdateTaskDto : IValidatableObject
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        [EnumDataType(typeof(AppTaskStatus))]
        public AppTaskStatus StatusId { get; set; }

        [Required]
        [EnumDataType(typeof(TaskPriority))]
        public TaskPriority PriorityId { get; set; }

        [Range(0, long.MaxValue)]
        public long? AssignedTo { get; set; }

        [Range(0, long.MaxValue)]
        public long? AssignedBy { get; set; }

        [Range(typeof(decimal), "0", "100000")]
        public decimal? EstimatedHours { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DueDate == default)
            {
                yield return new ValidationResult(
                    "Due date is required.",
                    new[] { nameof(DueDate) });
            }
        }
    }
}
