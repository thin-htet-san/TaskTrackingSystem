using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaskTrackingSystem.Shared.Models.Project
{
    public class AssignMembersDto
    {
        [Required]
        public List<long> UserIds { get; set; } = new List<long>();
    }
}
