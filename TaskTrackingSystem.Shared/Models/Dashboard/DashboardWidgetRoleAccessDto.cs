namespace TaskTrackingSystem.Shared.Models.Dashboard
{
    public class DashboardWidgetRoleAccessDto
    {
        public long RoleId { get; set; }

        public string RoleName { get; set; } = string.Empty;

        public bool CanView { get; set; }

        public bool CanConfigure { get; set; }

        public bool IsDefaultVisible { get; set; }

        public int DefaultGridX { get; set; }

        public int DefaultGridY { get; set; }

        public int DefaultWidth { get; set; }

        public int DefaultHeight { get; set; }

        public int DefaultSortOrder { get; set; }
    }
}
