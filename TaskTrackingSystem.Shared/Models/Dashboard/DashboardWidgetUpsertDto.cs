using System.ComponentModel.DataAnnotations;

namespace TaskTrackingSystem.Shared.Models.Dashboard
{
    public class DashboardWidgetUpsertDto
    {
        public long WidgetId { get; set; }

        [Required, MaxLength(50)]
        public string WidgetCode { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string WidgetName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        [MaxLength(100)]
        public string? ComponentKey { get; set; }

        [MaxLength(100)]
        public string? DataSourceKey { get; set; }

        public int DefaultWidth { get; set; } = 4;

        public int DefaultHeight { get; set; } = 3;

        public int DefaultOrder { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
