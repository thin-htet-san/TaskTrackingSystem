namespace TaskTrackingSystem.Shared.Models.Dashboard
{
    public class DashboardWidgetDto
    {
        public long WidgetId { get; set; }

        public string WidgetCode { get; set; } = string.Empty;

        public string WidgetName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Category { get; set; }

        public string? ComponentKey { get; set; }

        public string? DataSourceKey { get; set; }

        public int DefaultWidth { get; set; }

        public int DefaultHeight { get; set; }

        public int DefaultOrder { get; set; }

        public bool CanView { get; set; }

        public bool CanConfigure { get; set; }

        public bool IsDefaultVisible { get; set; }

        public bool HasCustomLayout { get; set; }

        public bool IsHidden { get; set; }

        public bool IsPinned { get; set; }

        public int GridX { get; set; }

        public int GridY { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int SortOrder { get; set; }

        public string? ConfigJson { get; set; }
    }
}
