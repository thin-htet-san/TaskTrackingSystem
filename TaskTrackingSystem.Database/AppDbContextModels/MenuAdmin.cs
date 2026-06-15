using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskTrackingSystem.Database.AppDbContextModels
{
    public partial class MenuAdmin
    {
        public string AdminMenuId { get; set; } = null!;

        public string MenuCode { get; set; } = null!;

        public string ParentCode { get; set; } = null!;

        public string MenuName { get; set; } = null!;

        public string? MenuUrl { get; set; }

        public bool Visible { get; set; }

        public int OrderNo { get; set; }

        public string? Icon { get; set; }

        public int DelFlag { get; set; }

        public string? CreatedUserId { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public string? ModifiedUserId { get; set; }
        public DateTime? ModifiedDateTime { get; set; }
    }
}

