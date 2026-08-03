using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskTrackingSystem.Shared.Models.User
{
    public class UpdateUserDto : IValidatableObject
    {
        [Required, MaxLength(50)]
        [MinLength(3, ErrorMessage = ResultMessages.UsernameMinLength)]
        [RegularExpression(@"^[a-zA-Z0-9._]+$", ErrorMessage = ResultMessages.UsernameInvalidCharacters)]
        public string Username { get; set; } = string.Empty;

        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? FirstNameMy { get; set; }

        [MaxLength(50)]
        public string? LastNameMy { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [Required]
        public long RoleId { get; set; }

        public bool IsActive { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var englishComplete = !string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(LastName);
            var burmeseComplete = !string.IsNullOrWhiteSpace(FirstNameMy) && !string.IsNullOrWhiteSpace(LastNameMy);
            if (!englishComplete && !burmeseComplete)
            {
                yield return new ValidationResult("At least one complete name is required.", new[] { nameof(FirstName), nameof(LastName), nameof(FirstNameMy), nameof(LastNameMy) });
            }
        }
    }
}
