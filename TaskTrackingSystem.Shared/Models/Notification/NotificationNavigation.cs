namespace TaskTrackingSystem.Shared.Models.Notification;

public static class NotificationNavigation
{
    public static string BuildTargetUrl(string sourceType, long sourceId, byte notificationType)
    {
        if (string.Equals(sourceType, "project", StringComparison.OrdinalIgnoreCase))
        {
            return $"/projects/{sourceId}/tasks";
        }

        if (string.Equals(sourceType, "task", StringComparison.OrdinalIgnoreCase))
        {
            var isCommentThread = notificationType is (byte)NotificationType.CommentAdded or (byte)NotificationType.Mention;
            return isCommentThread
                ? $"/tasks/{sourceId}/details?tab=comments"
                : $"/tasks/{sourceId}/details";
        }

        return "/dashboard";
    }
}
