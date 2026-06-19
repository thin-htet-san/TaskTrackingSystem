using System;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class UserDashboardLayout
{
    public long UserDashboardLayoutId { get; set; }

    public long UserId { get; set; }

    public long WidgetId { get; set; }

    public int GridX { get; set; }

    public int GridY { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public int SortOrder { get; set; }

    public bool IsHidden { get; set; }

    public bool IsPinned { get; set; }

    public string? ConfigJson { get; set; }

    public bool IsDeleted { get; set; }

    public long? CreatedById { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? UpdatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual DashboardWidget Widget { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
