using System;
using System.Collections.Generic;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class AdminMenu
{
    public string AdminMenuId { get; set; } = null!;

    public string MenuCode { get; set; } = null!;

    public string ParentCode { get; set; } = null!;

    public string MenuName { get; set; } = null!;

    public string? MenuUrl { get; set; }

    public bool Visible { get; set; }

    public int OrderNo { get; set; }

    public string? Icon { get; set; }

    public int DelFlag { get; set; }
}
