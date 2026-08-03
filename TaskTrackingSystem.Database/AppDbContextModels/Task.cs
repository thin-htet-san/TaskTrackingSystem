using System;
using System.Collections.Generic;
using TaskTrackingSystem.Shared.Enums;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class Task
{
    public long Id { get; set; }

    public string Title { get; set; } = null!;

    public string? TitleMy { get; set; }

    public string? Description { get; set; }

    public string? DescriptionMy { get; set; }

    public long ProjectId { get; set; }

    public AppTaskStatus StatusId { get; set; }

    public TaskPriority PriorityId { get; set; }

    public long? AssignedTo { get; set; }

    public long? AssignedBy { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public bool IsArchived { get; set; }

    public virtual User? AssignedByNavigation { get; set; }

    public virtual User? AssignedToNavigation { get; set; }

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual Project Project { get; set; } = null!;

}
