using System;
using System.Collections.Generic;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class Menu
{
    public long MenuId { get; set; }

    public string MenuCode { get; set; } = null!;

    public long? ParentMenuId { get; set; }

    public string MenuName { get; set; } = null!;

    public string? MenuNameMy { get; set; }

    public string? MenuUrl { get; set; }

    public string? Icon { get; set; }

    public bool Visible { get; set; }

    public int OrderNo { get; set; }

    public bool IsDeleted { get; set; }

    public long? CreatedById { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? UpdatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Menu> InverseParentMenu { get; set; } = new List<Menu>();

    public virtual Menu? ParentMenu { get; set; }

    public virtual ICollection<Permission> Permissions { get; set; } = new List<Permission>();

    public virtual ICollection<RoleMenu> RoleMenus { get; set; } = new List<RoleMenu>();
}
