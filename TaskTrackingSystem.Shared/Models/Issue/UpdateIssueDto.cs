using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TaskTrackingSystem.Shared.Enums;
using AppTaskStatus = TaskTrackingSystem.Shared.Enums.AppTaskStatus;

namespace TaskTrackingSystem.Shared.Models.Issue
{
    public class UpdateIssueDto : IValidatableObject
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Range(0, long.MaxValue)]
        public long? AssignedTo { get; set; }

        [Range(typeof(decimal), "0", "100000")]
        public decimal? EstimatedHours { get; set; }

        [Range(typeof(decimal), "0", "100000")]
        public decimal? ActualHours { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [EnumDataType(typeof(AppTaskStatus))]
        public AppTaskStatus StatusId { get; set; }

        [EnumDataType(typeof(TaskPriority))]
        public TaskPriority PriorityId { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartDate == default)
            {
                yield return new ValidationResult("Start date is required.", new[] { nameof(StartDate) });
            }

            if (DueDate == default)
            {
                yield return new ValidationResult("Due date is required.", new[] { nameof(DueDate) });
            }

            if (StartDate != default && DueDate != default && DueDate.Date < StartDate.Date)
            {
                yield return new ValidationResult("Due date cannot be earlier than start date.", new[] { nameof(DueDate), nameof(StartDate) });
            }
        }
    }
}
