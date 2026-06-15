using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaskTrackingSystem.Shared.Models.Role
{
    public class AssignMenusDto
    {
        [Required]
        public List<string> MenuCodes { get; set; } = new List<string>();
    }
}
