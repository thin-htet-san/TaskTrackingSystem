using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared.Models.Menu;
using MenuEntity = TaskTrackingSystem.Database.AppDbContextModels.Menu;

namespace TaskTrackingSystem.WebApi.Features.Menu
{
    public class MenuService
    {
        private readonly AppDbContext _db;

        public MenuService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<MenuDto>> GetMenuTreeByRoleIdAsync(long roleId)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.IsDeleted != true);
            if (role == null)
            {
                return new List<MenuDto>();
            }

            return await GetMenuTreeForRoleAsync(role.Id);
        }

        public async Task<List<MenuDto>> GetMenuTreeByRoleNameAsync(string roleName)
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

            return await GetMenuTreeForRoleAsync(role.Id);
        }

        private async Task<List<MenuDto>> GetMenuTreeForRoleAsync(long roleId)
        {
            var visibleMenus = await _db.Menus
                .Where(m => !m.IsDeleted && m.Visible)
                .OrderBy(m => m.OrderNo)
                .ToListAsync();

            var assignedMenuIds = await _db.RoleMenus
                .Where(rm => rm.RoleId == roleId && !rm.IsDeleted)
                .Select(rm => rm.MenuId)
                .ToListAsync();

            var assignedPermissionMenuIds = await _db.RolePermissions
                .Where(rp => rp.RoleId == roleId && !rp.IsDeleted)
                .Select(rp => rp.Permission.MenuId)
                .ToListAsync();

            var allowedMenuIds = assignedMenuIds
                .Concat(assignedPermissionMenuIds)
                .Distinct()
                .ToHashSet();

            if (allowedMenuIds.Count == 0)
            {
                return new List<MenuDto>();
            }

            var visibleMenuLookup = visibleMenus.ToDictionary(m => m.MenuId);
            var expandedMenuIds = ExpandWithAncestors(allowedMenuIds, visibleMenuLookup);

            var filteredMenus = visibleMenus
                .Where(m => expandedMenuIds.Contains(m.MenuId))
                .OrderBy(m => m.OrderNo)
                .ToList();

            return BuildHierarchy(filteredMenus);
        }

        private static List<MenuDto> BuildHierarchy(List<MenuEntity> menus)
        {
            if (menus.Count == 0)
            {
                return new List<MenuDto>();
            }

            var menuLookup = menus.ToDictionary(m => m.MenuId);
            var dtoLookup = menus.ToDictionary(
                m => m.MenuId,
                m => new MenuDto
                {
                    MenuCode = m.MenuCode,
                    ParentCode = GetParentCode(m, menuLookup),
                    MenuName = m.MenuName,
                    MenuUrl = m.MenuUrl,
                    OrderNo = m.OrderNo,
                    Icon = m.Icon
                });

            foreach (var dto in dtoLookup.Values)
            {
                dto.SubMenus = new List<MenuDto>();
            }

            var roots = new List<MenuDto>();

            foreach (var menu in menus.OrderBy(m => m.OrderNo).ThenBy(m => m.MenuName, StringComparer.OrdinalIgnoreCase))
            {
                var dto = dtoLookup[menu.MenuId];
                var parentId = menu.ParentMenuId;

                if (!parentId.HasValue || !dtoLookup.TryGetValue(parentId.Value, out var parentDto))
                {
                    roots.Add(dto);
                    continue;
                }

                parentDto.SubMenus.Add(dto);
            }

            SortMenuTree(roots);
            return roots;
        }

        private static string GetParentCode(MenuEntity menu, IReadOnlyDictionary<long, MenuEntity> menuLookup)
        {
            if (!menu.ParentMenuId.HasValue)
            {
                return string.Empty;
            }

            return menuLookup.TryGetValue(menu.ParentMenuId.Value, out var parent)
                ? parent.MenuCode
                : string.Empty;
        }

        private static HashSet<long> ExpandWithAncestors(IEnumerable<long> menuIds, IReadOnlyDictionary<long, MenuEntity> menuLookup)
        {
            var expanded = new HashSet<long>();

            foreach (var menuId in menuIds)
            {
                var currentId = menuId;

                while (currentId > 0 && expanded.Add(currentId))
                {
                    if (!menuLookup.TryGetValue(currentId, out var currentMenu) || !currentMenu.ParentMenuId.HasValue)
                    {
                        break;
                    }

                    currentId = currentMenu.ParentMenuId.Value;
                }
            }

            return expanded;
        }

        private static void SortMenuTree(IList<MenuDto> menus)
        {
            var ordered = menus
                .OrderBy(m => m.OrderNo)
                .ThenBy(m => m.MenuName, StringComparer.OrdinalIgnoreCase)
                .ToList();

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

        public async Task<string> GetMenuVersionAsync(long roleId, string roleName)
        {
            if (roleId <= 0 && !string.IsNullOrWhiteSpace(roleName))
            {
                var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName && r.IsDeleted != true);
                if (role != null)
                {
                    roleId = role.Id;
                }
            }

            if (roleId <= 0)
            {
                return "0";
            }

            var latestRoleMenuChange = await _db.RoleMenus
                .Where(rm => rm.RoleId == roleId && !rm.IsDeleted)
                .Select(rm => (DateTime?)(rm.UpdatedAt ?? rm.CreatedAt))
                .MaxAsync();

            var latestPermissionChange = await _db.RolePermissions
                .Where(rp => rp.RoleId == roleId && !rp.IsDeleted)
                .Select(rp => (DateTime?)(rp.UpdatedAt ?? rp.CreatedAt))
                .MaxAsync();

            var latestMenuMetadataChange = await _db.Menus
                .Select(m => (DateTime?)(m.UpdatedAt ?? m.CreatedAt))
                .MaxAsync();

            var latest = latestRoleMenuChange ?? latestPermissionChange;
            if (latestPermissionChange.HasValue && (!latest.HasValue || latestPermissionChange.Value > latest.Value))
            {
                latest = latestPermissionChange;
            }
            if (latestMenuMetadataChange.HasValue && (!latest.HasValue || latestMenuMetadataChange.Value > latest.Value))
            {
                latest = latestMenuMetadataChange;
            }

            return latest.HasValue ? latest.Value.Ticks.ToString() : "0";
        }

        public async Task<List<AccessMenuDto>> GetAllAccessItemsAsync()
        {
            var menus = await _db.Menus
                .Where(m => !m.IsDeleted && m.MenuCode != "ROLE_LAYOUTS" && m.MenuName != "Role Layouts")
                .OrderBy(m => m.OrderNo)
                .ToListAsync();

            var permissions = await _db.Permissions
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.OrderNo)
                .ToListAsync();

            var menuLookup = menus.ToDictionary(m => m.MenuId);
            var menuDtoLookup = menus
                .Select(m => new
                {
                    m.MenuId,
                    Dto = new AccessMenuDto
                    {
                        MenuCode = m.MenuCode,
                        ParentCode = GetParentCode(m, menuLookup),
                        MenuName = m.MenuName,
                        MenuUrl = m.MenuUrl,
                        OrderNo = m.OrderNo,
                        Icon = m.Icon,
                        Visible = m.Visible
                    }
                })
                .ToList();

            var menuDtos = menuDtoLookup.Select(x => x.Dto).ToList();
            var dtoLookup = menuDtoLookup.ToDictionary(x => x.MenuId, x => x.Dto);

            foreach (var permissionGroup in permissions.GroupBy(p => p.MenuId))
            {
                if (!dtoLookup.TryGetValue(permissionGroup.Key, out var dto))
                {
                    continue;
                }

                foreach (var permission in permissionGroup.OrderBy(p => p.OrderNo).ThenBy(p => p.ActionName, StringComparer.OrdinalIgnoreCase))
                {
                    dto.Permissions.Add(new AccessPermissionDto
                    {
                        PermissionId = permission.PermissionId.ToString(),
                        PermissionCode = permission.PermissionCode,
                        ParentMenuCode = menuLookup.TryGetValue(permission.MenuId, out var parentMenu) ? parentMenu.MenuCode : string.Empty,
                        ActionName = permission.ActionName,
                        ApiName = permission.ApiName,
                        Visible = permission.Visible,
                        OrderNo = permission.OrderNo
                    });
                }
            }

            return menuDtos;
        }

        public async Task<List<string>> GetCurrentAccessCodesAsync(long roleId, string roleName)
        {
            if (roleId <= 0 && !string.IsNullOrWhiteSpace(roleName))
            {
                var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName && r.IsDeleted != true);
                if (role != null)
                {
                    roleId = role.Id;
                }
            }

            if (roleId <= 0)
            {
                return new List<string>();
            }

            var validMenuCodes = await _db.Menus
                .Where(m => !m.IsDeleted)
                .Select(m => m.MenuCode)
                .ToListAsync();

            var validPermissionCodes = await _db.Permissions
                .Where(p => !p.IsDeleted)
                .Select(p => p.PermissionCode)
                .ToListAsync();

            var validCodes = validMenuCodes
                .Concat(validPermissionCodes)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var menuCodes = await _db.RoleMenus
                .Where(rm => rm.RoleId == roleId && !rm.IsDeleted)
                .Select(rm => rm.Menu.MenuCode)
                .ToListAsync();

            var permissionCodes = await _db.RolePermissions
                .Where(rp => rp.RoleId == roleId && !rp.IsDeleted)
                .Select(rp => rp.Permission.PermissionCode)
                .ToListAsync();

            return menuCodes
                .Concat(permissionCodes)
                .Where(code => validCodes.Contains(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}



