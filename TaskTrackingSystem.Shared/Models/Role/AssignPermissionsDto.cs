using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaskTrackingSystem.Shared.Models.Role
{
    public class AssignPermissionsDto
    {
        [Required]
        public List<long> PermissionIds { get; set; } = new List<long>();
    }
}
