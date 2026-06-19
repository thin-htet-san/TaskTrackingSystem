using System;

namespace TaskTrackingSystem.Shared.Models.Dashboard
{
    public class DashboardWidgetAdminDto
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

        public bool IsActive { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int RoleCount { get; set; }
    }
}
