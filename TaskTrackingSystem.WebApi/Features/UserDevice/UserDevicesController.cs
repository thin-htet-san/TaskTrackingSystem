using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Notification;
using TaskTrackingSystem.WebApi.Infrastructure;

namespace TaskTrackingSystem.WebApi.Features.UserDevice;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UserDevicesController : ControllerBase
{
    private readonly UserDeviceService _userDeviceService;

    public UserDevicesController(UserDeviceService userDeviceService)
    {
        _userDeviceService = userDeviceService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<Result>> Register([FromBody] RegisterDeviceTokenDto dto)
    {
        var userId = User.GetUserId();
        if (userId <= 0)
        {
            return Unauthorized(Result.Failure("User is not authenticated.", 401));
        }

        var result = await _userDeviceService.RegisterTokenAsync(userId, dto.FcmToken);
        return StatusCode(result.StatusCode, result);
    }

}
