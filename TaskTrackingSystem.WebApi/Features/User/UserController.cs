using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.User;
using TaskTrackingSystem.WebApi.Features.User;

namespace TaskTrackingSystem.WebApi.Features.User
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;

        public UserController(UserService userService)
        {
            _userService = userService;
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
            long? currentUserId = null;
            var result = await _userService.CreateUserAsync(createUserDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Result>> UpdateUser(long id, [FromBody] UpdateUserDto updateUserDto)
        {
            long? currentUserId = null;
            var result = await _userService.UpdateUserAsync(id, updateUserDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<Result>> DeleteUser(long id)
        {
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
