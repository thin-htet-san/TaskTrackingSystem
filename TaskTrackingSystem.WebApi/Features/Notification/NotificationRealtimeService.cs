using Microsoft.AspNetCore.SignalR;
using TaskTrackingSystem.Shared.Models.Notification;

namespace TaskTrackingSystem.WebApi.Features.Notification;

public class NotificationRealtimeService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationRealtimeService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async global::System.Threading.Tasks.Task SendCreatedAsync(long recipientId, NotificationDto notification, int unreadCount)
    {
        var group = _hubContext.Clients.Group(NotificationHub.GetUserGroupName(recipientId));
        await group.SendAsync("NotificationCreated", notification);
        await group.SendAsync("UnreadCountChanged", unreadCount);
    }

    public global::System.Threading.Tasks.Task SendUnreadCountAsync(long recipientId, int unreadCount)
    {
        return _hubContext.Clients
            .Group(NotificationHub.GetUserGroupName(recipientId))
            .SendAsync("UnreadCountChanged", unreadCount);
    }

    public global::System.Threading.Tasks.Task SendReadAsync(long recipientId, long notificationId, int unreadCount)
    {
        return _hubContext.Clients
            .Group(NotificationHub.GetUserGroupName(recipientId))
            .SendAsync("NotificationRead", notificationId, unreadCount);
    }
}
