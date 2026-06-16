using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using TaskTrackingSystem.Shared.Models.Menu;
using TaskTrackingSystem.WebApi.Infrastructure;

namespace TaskTrackingSystem.WebApi.Features.Menu
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MenuController : ControllerBase
    {
        private readonly MenuService _menuService;

        public MenuController(MenuService menuService)
        {
            _menuService = menuService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MenuDto>>> GetMenus()
        {
            var roleId = User.GetRoleId();
            if (roleId > 0)
            {
                var roleMenus = await _menuService.GetMenusByRoleIdAsync(roleId);
                return Ok(roleMenus);
            }

            var roleName = User.GetRoleName();
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return Forbid();
            }

            var roleMenusByName = await _menuService.GetMenusByRoleNameAsync(roleName);
            return Ok(roleMenusByName);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<MenuAdminDto>>> GetAllMenus()
        {
            var menus = await _menuService.GetAllMenusAsync();
            return Ok(menus);
        }

        /// <summary>
        /// Returns a lightweight version string for the current user's role permissions.
        /// Clients poll this to detect when an admin has changed permissions without fetching the full menu list.
        /// </summary>
        [HttpGet("version")]
        public async Task<ActionResult<string>> GetMenuVersion()
        {
            var roleId = User.GetRoleId();
            var roleName = User.GetRoleName() ?? string.Empty;
            var version = await _menuService.GetMenuVersionAsync(roleId, roleName);
            return Ok(version);
        }
    }
}
