using System;
using System.Collections.Generic;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class RoleMenu
{
    public string RoleMenuId { get; set; } = null!;

    public long RoleId { get; set; }

    public string? RoleCode { get; set; }

    public string MenuCode { get; set; } = null!;

    public int DelFlag { get; set; }

    public string? CreatedUserId { get; set; }

    public DateTime CreatedDateTime { get; set; }

    public string? ModifiedUserId { get; set; }

    public DateTime? ModifiedDateTime { get; set; }
}
