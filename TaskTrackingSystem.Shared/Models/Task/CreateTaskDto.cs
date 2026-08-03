using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TaskTrackingSystem.Shared.Enums;
using AppTaskStatus = TaskTrackingSystem.Shared.Enums.AppTaskStatus;

namespace TaskTrackingSystem.Shared.Models.Task
{
    public class CreateTaskDto : IValidatableObject
    {
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? TitleMy { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(1000)]
        public string? DescriptionMy { get; set; }

        [Required]
        [Range(1, long.MaxValue)]
        public long ProjectId { get; set; }

        [EnumDataType(typeof(AppTaskStatus))]
        public AppTaskStatus StatusId { get; set; }

        [EnumDataType(typeof(TaskPriority))]
        public TaskPriority PriorityId { get; set; }

        [Range(0, long.MaxValue)]
        public long? AssignedTo { get; set; }

        [Range(0, long.MaxValue)]
        public long? AssignedBy { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(TitleMy))
            {
                yield return new ValidationResult("At least one task title is required.", new[] { nameof(Title), nameof(TitleMy) });
            }

            if (DueDate == default)
            {
                yield return new ValidationResult(
                    "Due date is required.",
                    new[] { nameof(DueDate) });
            }
        }
    }
}
