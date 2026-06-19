using System;
using System.Collections.Generic;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class DashboardWidget
{
    public long WidgetId { get; set; }

    public string WidgetCode { get; set; } = null!;

    public string WidgetName { get; set; } = null!;

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

    public virtual ICollection<RoleDashboardWidget> RoleDashboardWidgets { get; set; } = new List<RoleDashboardWidget>();

    public virtual ICollection<UserDashboardLayout> UserDashboardLayouts { get; set; } = new List<UserDashboardLayout>();
}
