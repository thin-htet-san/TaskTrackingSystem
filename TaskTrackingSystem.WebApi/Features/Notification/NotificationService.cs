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

        await EnrichLocalizedTextAsync(items);

        foreach (var item in items)
        {
            item.TargetUrl = NotificationNavigation.BuildTargetUrl(item.SourceType, item.SourceId, item.NotificationType);
        }

        return items;
    }

    private async global::System.Threading.Tasks.Task EnrichLocalizedTextAsync(List<NotificationDto> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var taskIds = items
            .Where(item => item.SourceType.Equals("task", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.SourceId)
            .Distinct()
            .ToList();
        var projectIds = items
            .Where(item => item.SourceType.Equals("project", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.SourceId)
            .Distinct()
            .ToList();

        var tasks = taskIds.Count == 0
            ? new Dictionary<long, (string Title, string? TitleMy)>()
            : await _db.Tasks
                .Where(task => taskIds.Contains(task.Id))
                .Select(task => new { task.Id, task.Title, task.TitleMy })
                .ToDictionaryAsync(task => task.Id, task => (task.Title, task.TitleMy));

        var projects = projectIds.Count == 0
            ? new Dictionary<long, (string Name, string? NameMy)>()
            : await _db.Projects
                .Where(project => projectIds.Contains(project.Id))
                .Select(project => new { project.Id, project.Name, project.NameMy })
                .ToDictionaryAsync(project => project.Id, project => (project.Name, project.NameMy));

        foreach (var item in items)
        {
            item.TitleMy = string.IsNullOrWhiteSpace(item.TitleMy)
                ? GetTitleMy((NotificationType)item.NotificationType)
                : item.TitleMy;

            if (!string.IsNullOrWhiteSpace(item.BodyMy))
            {
                continue;
            }

            if (item.SourceType.Equals("task", StringComparison.OrdinalIgnoreCase)
                && tasks.TryGetValue(item.SourceId, out var task))
            {
                var taskTitle = string.IsNullOrWhiteSpace(task.TitleMy) ? task.Title : task.TitleMy;
                item.BodyMy = (NotificationType)item.NotificationType switch
                {
                    NotificationType.TaskAssigned => $"သင့်အား လုပ်ငန်းသစ်တစ်ခု တာဝန်ပေးအပ်ထားပါသည် - {taskTitle}",
                    NotificationType.CommentAdded => $"{GetSenderNameMy(item)} သည် လုပ်ငန်း '{taskTitle}' တွင် မှတ်ချက်တစ်ခု ရေးသားခဲ့ပါသည်",
                    NotificationType.Mention => $"{GetSenderNameMy(item)} သည် လုပ်ငန်း '{taskTitle}' တွင် သင့်ကို ရည်ညွှန်းဖော်ပြခဲ့ပါသည်",
                    _ => item.BodyMy
                };
            }

            if (item.SourceType.Equals("project", StringComparison.OrdinalIgnoreCase)
                && projects.TryGetValue(item.SourceId, out var project)
                && (NotificationType)item.NotificationType == NotificationType.ProjectUpdated)
            {
                var projectName = string.IsNullOrWhiteSpace(project.NameMy) ? project.Name : project.NameMy;
                item.BodyMy = $"သင့်အား စီမံကိန်း '{projectName}' တွင် တာဝန်ပေးအပ်ထားပါသည်";
            }
        }
    }

    private static string? GetTitleMy(NotificationType notificationType) => notificationType switch
    {
        NotificationType.TaskAssigned => "လုပ်ငန်းတာဝန် ပေးအပ်ခြင်း",
        NotificationType.StatusChanged => "လုပ်ငန်းအခြေအနေ ပြောင်းလဲခြင်း",
        NotificationType.ProjectUpdated => "စီမံကိန်းတာဝန် ပေးအပ်ခြင်း",
        NotificationType.CommentAdded => "မှတ်ချက်အသစ် ရေးသားခြင်း",
        NotificationType.Mention => "သင့်ကို ရည်ညွှန်းဖော်ပြခြင်း",
        _ => null
    };

    private static string GetSenderNameMy(NotificationDto item)
    {
        if (!string.IsNullOrWhiteSpace(item.SenderNameMy))
        {
            return item.SenderNameMy.Trim();
        }

        if (string.Equals(item.SenderName, "System Admin", StringComparison.OrdinalIgnoreCase))
        {
            return "စနစ်အုပ်ချုပ်သူ";
        }

        return string.IsNullOrWhiteSpace(item.SenderName) ? "စနစ်" : item.SenderName.Trim();
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
