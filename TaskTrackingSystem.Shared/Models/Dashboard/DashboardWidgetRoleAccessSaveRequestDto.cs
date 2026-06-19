using System.Collections.Generic;

namespace TaskTrackingSystem.Shared.Models.Dashboard
{
    public class DashboardWidgetRoleAccessSaveRequestDto
    {
        public List<DashboardWidgetRoleAccessDto> RoleAccess { get; set; } = new();
    }
}
