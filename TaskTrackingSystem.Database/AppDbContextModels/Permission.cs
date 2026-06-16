using System;
using System.Collections.Generic;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class Permission
{
    public long PermissionId { get; set; }

    public string PermissionCode { get; set; } = null!;

    public long MenuId { get; set; }

    public string ActionName { get; set; } = null!;

    public string ApiName { get; set; } = null!;

    public string? HttpMethod { get; set; }

    public bool Visible { get; set; }

    public int OrderNo { get; set; }

    public bool IsDeleted { get; set; }

    public long? CreatedById { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? UpdatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Menu Menu { get; set; } = null!;

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
