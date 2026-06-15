using System;
using System.Collections.Generic;

namespace TaskTrackingSystem.Shared.Models.Menu
{
    public class MenuDto
    {
        public string MenuCode { get; set; } = string.Empty;
        public string ParentCode { get; set; } = string.Empty;
        public string MenuName { get; set; } = string.Empty;
        public string? MenuUrl { get; set; }
        public int OrderNo { get; set; }
        public string? Icon { get; set; }
        public List<MenuDto> SubMenus { get; set; } = new List<MenuDto>();
    }
}
