using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaskTrackingSystem.Shared.Models.Role
{
    public class AssignMenusDto
    {
        [Required]
        /// <summary>
        /// Menu codes and permission codes selected for the role.
        /// </summary>
        public List<string> MenuCodes { get; set; } = new List<string>();
    }
}
