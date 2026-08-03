using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskTrackingSystem.Shared.Models.User
{
        public class CreateUserDto : IValidatableObject
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

            [Required, EmailAddress, MaxLength(256)]
            public string Email { get; set; } = string.Empty;

            [Required]
            [MinLength(8, ErrorMessage = ResultMessages.PasswordMinLengthRule)]
            [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$", ErrorMessage = ResultMessages.PasswordComplexityRule)]
            public string Password { get; set; } = string.Empty;

            [MaxLength(20)]
            public string? Phone { get; set; }

            [Required]
            public long RoleId { get; set; }

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
