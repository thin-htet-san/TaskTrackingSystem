using System;
using System.Collections.Generic;
using TaskTrackingSystem.Shared.Enums;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class Issue
{
    public long Id { get; set; }

    public long TaskId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public long? AssignedTo { get; set; }

    public decimal? EstimatedHours { get; set; }

    public decimal? ActualHours { get; set; }

    public string? DelayReason { get; set; }

    public bool IsBlocked { get; set; }

    public string? BlockedBy { get; set; }

    public int EscalationLevel { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime DueDate { get; set; }

    public AppTaskStatus StatusId { get; set; }

    public TaskPriority PriorityId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual User? AssignedToNavigation { get; set; }

    public virtual Task Task { get; set; } = null!;
}
