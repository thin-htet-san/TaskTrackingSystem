using System;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class RoleDashboardWidget
{
    public long RoleDashboardWidgetId { get; set; }

    public long RoleId { get; set; }

    public long WidgetId { get; set; }

    public bool CanView { get; set; }

    public bool CanConfigure { get; set; }

    public bool IsDefaultVisible { get; set; }

    public int DefaultGridX { get; set; }

    public int DefaultGridY { get; set; }

    public int DefaultWidth { get; set; }

    public int DefaultHeight { get; set; }

    public int DefaultSortOrder { get; set; }

    public bool IsDeleted { get; set; }

    public long? CreatedById { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? UpdatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual DashboardWidget Widget { get; set; } = null!;

    public virtual Role Role { get; set; } = null!;
}
