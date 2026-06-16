using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.User;
using TaskTrackingSystem.WebApi.Features.User;
using TaskTrackingSystem.WebApi.Infrastructure;

namespace TaskTrackingSystem.WebApi.Features.User
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly PermissionAuthorizationService _permissionAuthorizationService;

        public UserController(UserService userService, PermissionAuthorizationService permissionAuthorizationService)
        {
            _userService = userService;
            _permissionAuthorizationService = permissionAuthorizationService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserDto>> GetUser(long id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { message = $"User with ID {id} not found." });
            }
            return Ok(user);
        }

        [HttpPost]
        public async Task<ActionResult<Result<UserDto>>> CreateUser([FromBody] CreateUserDto createUserDto)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/User", "Create"))
            {
                return Forbid();
            }

            long? currentUserId = null;
            var result = await _userService.CreateUserAsync(createUserDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Result>> UpdateUser(long id, [FromBody] UpdateUserDto updateUserDto)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/User", "Update"))
            {
                return Forbid();
            }

            long? currentUserId = null;
            var result = await _userService.UpdateUserAsync(id, updateUserDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<Result>> DeleteUser(long id)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/User", "Delete"))
            {
                return Forbid();
            }

            long? loggedInUserId = null;
            var nameIdentifier = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (nameIdentifier != null && long.TryParse(nameIdentifier, out var parsedId))
            {
                loggedInUserId = parsedId;
            }

            var result = await _userService.SoftDeleteUserAsync(id, loggedInUserId);
            return StatusCode(result.StatusCode, result);
        }
    }
}
