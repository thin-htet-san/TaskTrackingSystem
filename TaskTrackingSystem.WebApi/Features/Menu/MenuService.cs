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

        public async Task<List<MenuDto>> GetMenusByRoleIdAsync(long roleId)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.IsDeleted != true);
            if (role == null)
            {
                return new List<MenuDto>();
            }

            return await GetMenusForRoleAsync(role.Id, role.Name);
        }

        public async Task<List<MenuDto>> GetMenusByRoleNameAsync(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return new List<MenuDto>();
            }

            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName && r.IsDeleted != true);
            if (role == null)
            {
                return new List<MenuDto>();
            }

            return await GetMenusForRoleAsync(role.Id, role.Name);
        }

        private async Task<List<MenuDto>> GetMenusForRoleAsync(long roleId, string roleName)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.IsDeleted != true);
            if (role == null)
            {
                return new List<MenuDto>();
            }

            var allVisibleMenus = await _db.MenuAdmins
                .Where(m => m.Visible && m.DelFlag == 0)
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

            // Keep compatibility with roles stored by either RoleId or the older RoleCode link.
            var allowedMenuCodes = await GetAllowedMenuCodesAsync(role.Id, role.Name);

            if (allowedMenuCodes.Count == 0)
            {
                return new List<MenuDto>();
            }

            var visibleMenuLookup = allVisibleMenus
                .GroupBy(m => m.MenuCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var expandedMenuCodes = ExpandWithAncestors(allowedMenuCodes, visibleMenuLookup);
            var menus = allVisibleMenus
                .Where(m => expandedMenuCodes.Contains(m.MenuCode))
                .OrderBy(m => m.OrderNo)
                .ToList();

            return BuildHierarchy(menus);
        }

        private static List<MenuDto> BuildHierarchy(List<MenuDto> menus)
        {
            if (menus.Count == 0)
            {
                return new List<MenuDto>();
            }

            var menuLookup = menus
                .GroupBy(m => m.MenuCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var menu in menuLookup.Values)
            {
                menu.SubMenus = new List<MenuDto>();
            }

            var roots = new List<MenuDto>();

            foreach (var menu in menuLookup.Values.OrderBy(m => m.OrderNo).ThenBy(m => m.MenuName))
            {
                var parentCode = NormalizeMenuCode(menu.ParentCode);
                if (string.IsNullOrWhiteSpace(parentCode) ||
                    parentCode == "0" ||
                    !menuLookup.TryGetValue(parentCode, out var parent))
                {
                    roots.Add(menu);
                    continue;
                }

                parent.SubMenus.Add(menu);
            }

            SortMenuTree(roots);
            return roots;
        }

        private async Task<HashSet<string>> GetAllowedMenuCodesAsync(long roleId, string roleName)
        {
            var allowedMenuCodes = await _db.RoleMenus
                .Where(rm => rm.RoleId == roleId && rm.DelFlag == 0)
                .Select(rm => rm.MenuCode)
                .ToListAsync();

            if (allowedMenuCodes.Count > 0)
            {
                return allowedMenuCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(roleName))
            {
                allowedMenuCodes = await _db.RoleMenus
                    .Where(rm => rm.RoleCode == roleName && rm.DelFlag == 0)
                    .Select(rm => rm.MenuCode)
                    .ToListAsync();
            }

            return allowedMenuCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static HashSet<string> ExpandWithAncestors(IEnumerable<string> menuCodes, IReadOnlyDictionary<string, MenuDto> menuLookup)
        {
            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var menuCode in menuCodes.Where(code => !string.IsNullOrWhiteSpace(code)))
            {
                var currentCode = menuCode.Trim();

                while (!string.IsNullOrWhiteSpace(currentCode) && expanded.Add(currentCode))
                {
                    if (!menuLookup.TryGetValue(currentCode, out var currentMenu))
                    {
                        break;
                    }

                    currentCode = NormalizeMenuCode(currentMenu.ParentCode);
                    if (string.IsNullOrWhiteSpace(currentCode) || currentCode == "0")
                    {
                        break;
                    }
                }
            }

            return expanded;
        }

        private static void SortMenuTree(IList<MenuDto> menus)
        {
            var ordered = menus.OrderBy(m => m.OrderNo).ThenBy(m => m.MenuName, StringComparer.OrdinalIgnoreCase).ToList();
            menus.Clear();

            foreach (var menu in ordered)
            {
                if (menu.SubMenus.Count > 0)
                {
                    SortMenuTree(menu.SubMenus);
                }

                menus.Add(menu);
            }
        }

        private static string NormalizeMenuCode(string? code)
        {
            return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim();
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
