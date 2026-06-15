using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Role;

namespace TaskTrackingSystem.WebApi.Features.Role
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class RoleController : ControllerBase
    {
        private readonly RoleService _roleService;

        public RoleController(RoleService roleService)
        {
            _roleService = roleService;
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
            long? currentUserId = null;
            var result = await _roleService.CreateRoleAsync(createRoleDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Result>> UpdateRole(long id, [FromBody] UpdateRoleDto updateRoleDto)
        {
            long? currentUserId = null;
            var result = await _roleService.UpdateRoleAsync(id, updateRoleDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<Result>> DeleteRole(long id)
        {
            var result = await _roleService.SoftDeleteRoleAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/permissions")]
        public async Task<IActionResult> AssignPermissions(long id, [FromBody] AssignPermissionsDto dto)
        {
            var result = await _roleService.AssignPermissionsToRoleAsync(id, dto);
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }
            return Ok();
        }

        [HttpGet("{id}/permissions")]
        public async Task<ActionResult<Result<List<long>>>> GetAssignedPermissions(long id)
        {
            var result = await _roleService.GetAssignedPermissionsByRoleIdAsync(id);
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }
            return Ok(result);
        }

        [HttpGet("{id}/menus")]
        public async Task<ActionResult<Result<List<string>>>> GetAssignedMenus(long id)
        {
            var result = await _roleService.GetAssignedMenusByRoleIdAsync(id);
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }
            return Ok(result);
        }

        [HttpPost("{id}/menus")]
        public async Task<IActionResult> AssignMenus(long id, [FromBody] AssignMenusDto dto)
        {
            var result = await _roleService.AssignMenusToRoleAsync(id, dto);
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }
            return Ok();
        }
    }
}
