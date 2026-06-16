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
        [HttpGet("tree")]
        public async Task<ActionResult<IEnumerable<MenuDto>>> GetMenuTree()
        {
            var roleId = User.GetRoleId();
            if (roleId > 0)
            {
                var menuTree = await _menuService.GetMenuTreeByRoleIdAsync(roleId);
                return Ok(menuTree);
            }

            var roleName = User.GetRoleName();
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return Forbid();
            }

            var menuTreeByName = await _menuService.GetMenuTreeByRoleNameAsync(roleName);
            return Ok(menuTreeByName);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("all")]
        [HttpGet("access-items")]
        public async Task<ActionResult<IEnumerable<AccessMenuDto>>> GetAllAccessItems()
        {
            var menus = await _menuService.GetAllAccessItemsAsync();
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



