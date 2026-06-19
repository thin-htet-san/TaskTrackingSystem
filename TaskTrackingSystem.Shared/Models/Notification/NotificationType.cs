namespace TaskTrackingSystem.Shared.Models.Notification;

public enum NotificationType : byte
{
    TaskAssigned = 1,
    StatusChanged = 2,
    DueDateReminder = 3,
    OverdueAlert = 4,
    CommentAdded = 5,
    Mention = 6,
    PriorityChanged = 7,
    ProjectUpdated = 8,
    System = 9
}
