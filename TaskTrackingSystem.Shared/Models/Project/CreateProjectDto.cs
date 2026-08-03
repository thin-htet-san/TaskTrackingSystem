using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaskTrackingSystem.Shared.Models.Project
{
    public class CreateProjectDto : IValidatableObject
    {
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? NameMy { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(500)]
        public string? DescriptionMy { get; set; }

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
            if (string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(NameMy))
            {
                yield return new ValidationResult("At least one project name is required.", new[] { nameof(Name), nameof(NameMy) });
            }

            if (EndDate < StartDate)
            {
                yield return new ValidationResult(
                    "End date cannot be before start date.",
                    new[] { nameof(EndDate), nameof(StartDate) });
            }
        }
    }
}
