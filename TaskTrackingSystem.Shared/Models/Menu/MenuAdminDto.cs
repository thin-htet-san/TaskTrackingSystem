namespace TaskTrackingSystem.Shared.Models.Menu
{
    public class MenuAdminDto
    {
        public string MenuCode { get; set; } = string.Empty;
        public string ParentCode { get; set; } = string.Empty;
        public string MenuName { get; set; } = string.Empty;
        public string? MenuUrl { get; set; }
        public int OrderNo { get; set; }
        public string? Icon { get; set; }
        public bool Visible { get; set; }
        public System.Collections.Generic.List<MenuAdminDetailDto> Actions { get; set; } = new();
    }
}
