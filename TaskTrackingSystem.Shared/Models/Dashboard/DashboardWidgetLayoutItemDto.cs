namespace TaskTrackingSystem.Shared.Models.Dashboard
{
    public class DashboardWidgetLayoutItemDto
    {
        public long WidgetId { get; set; }

        public int GridX { get; set; }

        public int GridY { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int SortOrder { get; set; }

        public bool IsHidden { get; set; }

        public bool IsPinned { get; set; }

        public string? ConfigJson { get; set; }
    }
}
