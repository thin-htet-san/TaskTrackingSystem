using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaskTrackingSystem.Shared.Models.Role
{
    public class AssignAccessDto
    {
        [Required]
        /// <summary>
        /// Menu codes and permission codes selected for the role.
        /// </summary>
        public List<string> AccessCodes { get; set; } = new List<string>();
    }
}

