using System;
using System.Collections.Generic;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class TaskHistory
{
    public long Id { get; set; }

    public long TaskId { get; set; }

    public long ModifiedById { get; set; }

    public long? OldStatusId { get; set; }

    public long? NewStatusId { get; set; }

    public long? OldPriorityId { get; set; }

    public long? NewPriorityId { get; set; }

    public string? Remarks { get; set; }

    public long CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual User ModifiedBy { get; set; } = null!;

    public virtual Task Task { get; set; } = null!;
}
