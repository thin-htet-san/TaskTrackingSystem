using System;

namespace TaskTrackingSystem.Shared.Models.Menu
{
    public class MenuAdminDetailDto
    {
        public string MenuAdminDetailId { get; set; } = string.Empty;
        public string MenuDetailCode { get; set; } = string.Empty;
        public string ParentMenuCode { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public string ApiName { get; set; } = string.Empty;
        public bool Visible { get; set; }
        public int OrderNo { get; set; }
    }
}
