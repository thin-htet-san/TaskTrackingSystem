using System.Collections.Generic;

namespace TaskTrackingSystem.Shared.Models.Dashboard
{
    public class DashboardWidgetLayoutSaveRequestDto
    {
        public List<DashboardWidgetLayoutItemDto> Widgets { get; set; } = new();
    }
}
