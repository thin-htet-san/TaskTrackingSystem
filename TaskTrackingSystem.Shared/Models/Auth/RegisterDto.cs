using System;
using System.ComponentModel.DataAnnotations;

namespace TaskTrackingSystem.Shared.Models.Auth
{
    public class RegisterDto
    {
        [Required, MaxLength(50)]
        [MinLength(3, ErrorMessage = ResultMessages.UsernameMinLength)]
        [RegularExpression(@"^[a-zA-Z0-9._]+$", ErrorMessage = ResultMessages.UsernameInvalidCharacters)]
        public string Username { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = ResultMessages.PasswordMinLengthRule)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$", ErrorMessage = ResultMessages.PasswordComplexityRule)]
        public string Password { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Phone { get; set; }
    }
}
