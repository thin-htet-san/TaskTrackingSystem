using System.ComponentModel.DataAnnotations;

namespace TaskTrackingSystem.Shared.Models.Auth
{
    public class ResetPasswordDto
    {
        [Required]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [Required]
        public string RecoveryCode { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = ResultMessages.PasswordMinLengthRule)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$", ErrorMessage = ResultMessages.PasswordComplexityRule)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
