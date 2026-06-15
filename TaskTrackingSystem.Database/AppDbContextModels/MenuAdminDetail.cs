using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskTrackingSystem.Database.AppDbContextModels
{
    public partial class MenuAdminDetail
    {
        public string MenuAdminDetailId { get; set; } = null!;

        public string MenuDetailCode { get; set; } = null!;

        public string ParentMenuCode { get; set; } = null!;

        public string ActionName { get; set; } = null!;

        public string ApiName { get; set; } = null!;

        public bool Visible { get; set; }

        public int OrderNo { get; set; }

        public int DelFlag { get; set; }

        public string? CreatedUserId { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public string? ModifiedUserId { get; set; }
        public DateTime? ModifiedDateTime { get; set; }
    }
}


