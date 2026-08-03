using System.ComponentModel.DataAnnotations;

namespace TaskTrackingSystem.Shared.Models.Role
{
    public class UpdateRoleDto : IValidatableObject
    {
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? NameMy { get; set; }

        [MaxLength(200)]
        public string? Description { get; set; }

        [MaxLength(200)]
        public string? DescriptionMy { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(NameMy))
            {
                yield return new ValidationResult("At least one role name is required.", new[] { nameof(Name), nameof(NameMy) });
            }
        }
    }
}
