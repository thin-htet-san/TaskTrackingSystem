using System;

namespace TaskTrackingSystem.Shared.Models.Notification;

public class NotificationDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? TitleMy { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? BodyMy { get; set; }
    public byte NotificationType { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public long SourceId { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? SenderName { get; set; }
    public string? SenderNameMy { get; set; }
}
