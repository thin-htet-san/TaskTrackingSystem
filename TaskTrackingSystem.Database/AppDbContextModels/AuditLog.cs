using System;
using System.Collections.Generic;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class AuditLog
{
    public long Id { get; set; }

    public long? UserId { get; set; }

    public string Action { get; set; } = null!;

    public string Module { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string? IpAddress { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
