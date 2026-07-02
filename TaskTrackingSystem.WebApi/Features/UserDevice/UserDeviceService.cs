using Microsoft.EntityFrameworkCore;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using DbUserDevice = TaskTrackingSystem.Database.AppDbContextModels.UserDevice;

namespace TaskTrackingSystem.WebApi.Features.UserDevice;

public class UserDeviceService
{
    private readonly AppDbContext _db;

    public UserDeviceService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> RegisterTokenAsync(long userId, string fcmToken)
    {
        var token = fcmToken.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure("FCM token is required.", 400);
        }

        var existing = await _db.UserDevices.FirstOrDefaultAsync(x => x.FcmToken == token);
        var now = DateTime.UtcNow;

        if (existing == null)
        {
            _db.UserDevices.Add(new DbUserDevice
            {
                UserId = userId,
                FcmToken = token,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existing.UserId = userId;
            existing.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
        return Result.Success();
    }

}
