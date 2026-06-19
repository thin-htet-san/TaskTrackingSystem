using Microsoft.EntityFrameworkCore;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Notification;
using DbNotification = TaskTrackingSystem.Database.AppDbContextModels.Notification;

namespace TaskTrackingSystem.WebApi.Features.Notification;

public class NotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetRecentAsync(long userId, int take = 10)
    {
        return await (
            from n in _db.Notifications
            where n.RecipientId == userId
            join sender in _db.Users on n.SenderId equals sender.Id into senderGroup
            from sender in senderGroup.DefaultIfEmpty()
            orderby n.CreatedAt descending
            select new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Body = n.Body,
                NotificationType = n.NotificationType,
                SourceType = n.SourceType,
                SourceId = n.SourceId,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt,
                SenderName = sender == null ? null : $"{sender.FirstName} {sender.LastName}"
            })
            .Take(take)
            .ToListAsync();
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
        }

        return Result.Success();
    }
}
