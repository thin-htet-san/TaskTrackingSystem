using System;
using System.Collections.Generic;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class TimeLog
{
    public long Id { get; set; }

    public long TaskId { get; set; }

    public long UserId { get; set; }

    public decimal HoursLogged { get; set; }

    public DateOnly LogDate { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual Task Task { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
