using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared.Models.Menu;

namespace TaskTrackingSystem.WebApi.Features.Menu
{
    public class MenuService
    {
        private readonly AppDbContext _db;

        public MenuService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<MenuDto>> GetMenusByRoleAsync(string roleName)
        {
            // 1. Get the MenuCodes configured for this Role
            var allowedMenuCodes = await _db.RoleMenus
                .Where(rm => rm.RoleCode == roleName && rm.DelFlag == 0)
                .Select(rm => rm.MenuCode)
                .ToListAsync();

            // 2. Retrieve the active menu items matching those codes
            var menus = await _db.MenuAdmins
                .Where(m => allowedMenuCodes.Contains(m.MenuCode) && m.Visible && m.DelFlag == 0)
                .OrderBy(m => m.OrderNo)
                .Select(m => new MenuDto
                {
                    MenuCode = m.MenuCode,
                    ParentCode = m.ParentCode,
                    MenuName = m.MenuName,
                    MenuUrl = m.MenuUrl,
                    OrderNo = m.OrderNo,
                    Icon = m.Icon
                })
                .ToListAsync();

            // 3. Construct the menu hierarchy (Parents -> SubMenus)
            var parentMenus = menus.Where(m => string.IsNullOrEmpty(m.ParentCode) || m.ParentCode == "0").ToList();
            var childMenus = menus.Where(m => !string.IsNullOrEmpty(m.ParentCode) && m.ParentCode != "0").ToList();

            foreach (var parent in parentMenus)
            {
                parent.SubMenus = childMenus
                    .Where(c => c.ParentCode == parent.MenuCode)
                    .OrderBy(c => c.OrderNo)
                    .ToList();
            }

            return parentMenus.OrderBy(p => p.OrderNo).ToList();
        }
        public async Task<List<MenuAdminDto>> GetAllMenusAsync()
        {
            var menus = await _db.MenuAdmins
                .Where(m => m.DelFlag == 0)
                .OrderBy(m => m.OrderNo)
                .Select(m => new MenuAdminDto
                {
                    MenuCode = m.MenuCode,
                    ParentCode = m.ParentCode,
                    MenuName = m.MenuName,
                    MenuUrl = m.MenuUrl,
                    OrderNo = m.OrderNo,
                    Icon = m.Icon,
                    Visible = m.Visible
                })
                .ToListAsync();

            var details = await _db.MenuAdminDetails
                .Where(d => d.DelFlag == 0)
                .OrderBy(d => d.OrderNo)
                .Select(d => new MenuAdminDetailDto
                {
                    MenuAdminDetailId = d.MenuAdminDetailId,
                    MenuDetailCode = d.MenuDetailCode,
                    ParentMenuCode = d.ParentMenuCode,
                    ActionName = d.ActionName,
                    ApiName = d.ApiName,
                    Visible = d.Visible,
                    OrderNo = d.OrderNo
                })
                .ToListAsync();

            foreach (var menu in menus)
            {
                menu.Actions = details
                    .Where(d => d.ParentMenuCode.Equals(menu.MenuCode, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return menus;
        }
    }
}
