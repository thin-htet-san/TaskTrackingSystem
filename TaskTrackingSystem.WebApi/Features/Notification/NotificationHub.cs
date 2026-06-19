using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TaskTrackingSystem.WebApi.Infrastructure;

namespace TaskTrackingSystem.WebApi.Features.Notification;

[Authorize]
public class NotificationHub : Hub
{
    public static string GetUserGroupName(long userId) => $"notification-user-{userId}";

    public override async global::System.Threading.Tasks.Task OnConnectedAsync()
    {
        var userId = Context.User?.GetUserId() ?? 0;
        if (userId > 0)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(userId));
        }

        await base.OnConnectedAsync();
    }

    public override async global::System.Threading.Tasks.Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.GetUserId() ?? 0;
        if (userId > 0)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetUserGroupName(userId));
        }

        await base.OnDisconnectedAsync(exception);
    }
}
