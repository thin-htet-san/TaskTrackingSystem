using System;
using System.Collections.Generic;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class RolePermission
{
    public long RolePermissionId { get; set; }

    public long RoleId { get; set; }

    public long PermissionId { get; set; }

    public bool IsDeleted { get; set; }

    public long? CreatedById { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? UpdatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Permission Permission { get; set; } = null!;

    public virtual Role Role { get; set; } = null!;
}
