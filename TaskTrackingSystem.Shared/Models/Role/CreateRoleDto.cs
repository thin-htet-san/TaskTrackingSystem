using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaskTrackingSystem.Shared.Models.Role
{
    public class CreateRoleDto
    {
        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        /// <summary>
        /// Optional: Menu and action codes to assign to this role on creation.
        /// </summary>
        public List<string> MenuCodes { get; set; } = new();
    }
}
