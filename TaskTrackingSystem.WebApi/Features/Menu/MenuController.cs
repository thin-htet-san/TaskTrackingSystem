using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using TaskTrackingSystem.Shared.Models.Menu;

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
            var roleToQuery = User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrWhiteSpace(roleToQuery))
            {
                return Forbid();
            }

            var menus = await _menuService.GetMenusByRoleAsync(roleToQuery);
            return Ok(menus);
        }
        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<MenuAdminDto>>> GetAllMenus()
        {
            var menus = await _menuService.GetAllMenusAsync();
            return Ok(menus);
        }
    }
}
