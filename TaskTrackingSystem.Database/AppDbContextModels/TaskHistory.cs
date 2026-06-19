using System;
using System.Collections.Generic;
using TaskTrackingSystem.Shared.Enums;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class TaskHistory
{
    public long Id { get; set; }

    public long TaskId { get; set; }

    public long ModifiedById { get; set; }

    public AppTaskStatus? OldStatusId { get; set; }

    public AppTaskStatus? NewStatusId { get; set; }

    public TaskPriority? OldPriorityId { get; set; }

    public TaskPriority? NewPriorityId { get; set; }

    public string? Remarks { get; set; }

    public long CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual User ModifiedBy { get; set; } = null!;

    public virtual Task Task { get; set; } = null!;
}
