using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Role;
using TaskTrackingSystem.WebApi.Infrastructure;

namespace TaskTrackingSystem.WebApi.Features.Role
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RoleController : ControllerBase
    {
        private readonly RoleService _roleService;
        private readonly PermissionAuthorizationService _permissionAuthorizationService;

        public RoleController(RoleService roleService, PermissionAuthorizationService permissionAuthorizationService)
        {
            _roleService = roleService;
            _permissionAuthorizationService = permissionAuthorizationService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RoleDto>>> GetRoles()
        {
            var roles = await _roleService.GetAllRolesAsync();
            return Ok(roles);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RoleDto>> GetRole(long id)
        {
            var role = await _roleService.GetRoleByIdAsync(id);
            if (role == null)
            {
                return NotFound(new { message = $"Role with ID {id} not found." });
            }
            return Ok(role);
        }

        [HttpPost]
        public async Task<ActionResult<Result<RoleDto>>> CreateRole([FromBody] CreateRoleDto createRoleDto)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Role", "Create"))
            {
                return Forbid();
            }

            long? currentUserId = User.GetUserId();
            var result = await _roleService.CreateRoleAsync(createRoleDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Result>> UpdateRole(long id, [FromBody] UpdateRoleDto updateRoleDto)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Role", "Update"))
            {
                return Forbid();
            }

            long? currentUserId = User.GetUserId();
            var result = await _roleService.UpdateRoleAsync(id, updateRoleDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<Result>> DeleteRole(long id)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Role", "Delete"))
            {
                return Forbid();
            }

            var result = await _roleService.SoftDeleteRoleAsync(id, User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }


        [HttpGet("{id}/menus")]
        [HttpGet("{id}/access")]
        public async Task<ActionResult<Result<List<string>>>> GetAssignedAccessCodes(long id)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Role", "Update"))
            {
                return Forbid();
            }

            var result = await _roleService.GetAssignedAccessCodesByRoleIdAsync(id);
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }
            return Ok(result);
        }

        [HttpPost("{id}/menus")]
        [HttpPost("{id}/access")]
        public async Task<IActionResult> AssignAccess(long id, [FromBody] AssignAccessDto dto)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Role", "Update"))
            {
                return Forbid();
            }

            var result = await _roleService.AssignAccessToRoleAsync(id, dto, User.GetUserId());
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }
            return Ok();
        }
    }
}



