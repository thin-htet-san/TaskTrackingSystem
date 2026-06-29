using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskTrackingSystem.Database.AppDbContextModels;

namespace TaskTrackingSystem.WebApi.Features.Notification;

public class NotificationCleanupHostedService : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationCleanupHostedService> _logger;

    public NotificationCleanupHostedService(IServiceScopeFactory scopeFactory, ILogger<NotificationCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RunInterval);

        await CleanupOnceAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupOnceAsync(stoppingToken);
        }
    }

    private async System.Threading.Tasks.Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cutoffUtc = DateTime.UtcNow.Subtract(RetentionWindow);

            var deletedCount = await db.Notifications
                .Where(n => n.IsRead && (!n.CreatedAt.HasValue || n.CreatedAt < cutoffUtc))
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation("Notification cleanup removed {DeletedCount} read notifications older than {CutoffUtc}.", deletedCount, cutoffUtc);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification cleanup failed.");
        }
    }
}
