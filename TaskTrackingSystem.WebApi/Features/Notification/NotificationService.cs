using Microsoft.EntityFrameworkCore;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Notification;
using DbNotification = TaskTrackingSystem.Database.AppDbContextModels.Notification;

namespace TaskTrackingSystem.WebApi.Features.Notification;

public class NotificationService
{
    private readonly AppDbContext _db;
    private readonly NotificationRealtimeService _realtimeService;

    public NotificationService(AppDbContext db, NotificationRealtimeService realtimeService)
    {
        _db = db;
        _realtimeService = realtimeService;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetRecentAsync(long userId, int take = 10)
    {
        var items = await (
            from n in _db.Notifications
            where n.RecipientId == userId
            join sender in _db.Users on n.SenderId equals sender.Id into senderGroup
            from sender in senderGroup.DefaultIfEmpty()
            orderby n.CreatedAt descending
            select new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                TitleMy = n.TitleMy,
                Body = n.Body,
                BodyMy = n.BodyMy,
                NotificationType = n.NotificationType,
                SourceType = n.SourceType,
                SourceId = n.SourceId,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt,
                SenderName = sender == null ? null : $"{sender.FirstName} {sender.LastName}",
                SenderNameMy = sender == null ? null : $"{sender.FirstNameMy} {sender.LastNameMy}"
            })
            .Take(take)
            .ToListAsync();

        foreach (var item in items)
        {
            item.TargetUrl = NotificationNavigation.BuildTargetUrl(item.SourceType, item.SourceId, item.NotificationType);
        }

        return items;
    }

    public async Task<int> GetUnreadCountAsync(long userId)
    {
        return await _db.Notifications.CountAsync(n => n.RecipientId == userId && !n.IsRead);
    }

    public async Task<Result> MarkReadAsync(long userId, long notificationId)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == userId);

        if (notification == null)
        {
            return Result.Failure("Notification not found.", 404);
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var unreadCount = await GetUnreadCountAsync(userId);
            await _realtimeService.SendReadAsync(userId, notificationId, unreadCount);
        }

        return Result.Success();
    }

    public async Task<int> DeleteReadNotificationsAsync(long userId, DateTime? olderThanUtc = null)
    {
        var query = _db.Notifications.Where(n => n.RecipientId == userId && n.IsRead);

        if (olderThanUtc.HasValue)
        {
            var cutoff = olderThanUtc.Value;
            query = query.Where(n => !n.CreatedAt.HasValue || n.CreatedAt < cutoff);
        }

        return await query.ExecuteDeleteAsync();
    }
}
