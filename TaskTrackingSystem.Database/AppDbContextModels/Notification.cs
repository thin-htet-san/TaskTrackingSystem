using System;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class Notification
{
    public long Id { get; set; }

    public long RecipientId { get; set; }

    public long? SenderId { get; set; }

    public byte NotificationType { get; set; }

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    public string SourceType { get; set; } = null!;

    public long SourceId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User Recipient { get; set; } = null!;

    public virtual User? Sender { get; set; }
}
