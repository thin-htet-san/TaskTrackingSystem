using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaskTrackingSystem.Shared.Models.Project
{
    public class CreateProjectDto : IValidatableObject
    {
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public long CreatedById { get; set; }

        [Range(0, 100000)]
        public int? BudgetedHours { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDate < StartDate)
            {
                yield return new ValidationResult(
                    "End date cannot be before start date.",
                    new[] { nameof(EndDate), nameof(StartDate) });
            }
        }
    }
}
