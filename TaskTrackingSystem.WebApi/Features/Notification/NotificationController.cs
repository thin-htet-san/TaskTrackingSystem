using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.WebApi.Infrastructure;

namespace TaskTrackingSystem.WebApi.Features.Notification;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly NotificationService _notificationService;

    public NotificationController(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<TaskTrackingSystem.Shared.Models.Notification.NotificationDto>>> GetMine([FromQuery] int take = 10)
    {
        var userId = User.GetUserId();
        if (userId <= 0)
        {
            return Unauthorized();
        }

        var items = await _notificationService.GetRecentAsync(userId, take);
        return Ok(items);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        var userId = User.GetUserId();
        if (userId <= 0)
        {
            return Unauthorized();
        }

        var count = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(count);
    }

    [HttpPost("{id}/read")]
    public async Task<ActionResult<Result>> MarkRead(long id)
    {
        var userId = User.GetUserId();
        if (userId <= 0)
        {
            return Unauthorized(Result.Failure("User is not authenticated.", 401));
        }

        var result = await _notificationService.MarkReadAsync(userId, id);
        return StatusCode(result.StatusCode, result);
    }
}
