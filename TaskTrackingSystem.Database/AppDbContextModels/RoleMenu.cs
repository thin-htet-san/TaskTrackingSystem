using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskTrackingSystem.Database.AppDbContextModels
{
    public partial class RoleMenu
    {
        public string RoleMenuId { get; set; } = null!;
        public string RoleCode { get; set; } = null!;
        public string MenuCode { get; set; } = null!;
        public int DelFlag { get; set; }

        public string? CreatedUserId { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public string? ModifiedUserId { get; set; }
        public DateTime? ModifiedDateTime { get; set; }
    }
}
