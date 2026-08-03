using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Role;
using TaskTrackingSystem.WebApi.Infrastructure;
using RoleEntity = TaskTrackingSystem.Database.AppDbContextModels.Role;

namespace TaskTrackingSystem.WebApi.Features.Role
{
    public class RoleService
    {
        private readonly AppDbContext _db;
        private readonly Infrastructure.AuditLogService _auditLog;

        public RoleService(AppDbContext db, Infrastructure.AuditLogService auditLog)
        {
            _db = db;
            _auditLog = auditLog;
        }

        public async Task<IEnumerable<RoleDto>> GetAllRolesAsync()
        {
            return await _db.Roles
                .Where(r => r.IsDeleted != true)
                .Select(r => new RoleDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    NameMy = r.NameMy,
                    Description = r.Description,
                    DescriptionMy = r.DescriptionMy,
                    CreatedAt = r.CreatedAt ?? DateTime.UtcNow
                })
                .ToListAsync();
        }

        public async Task<PagedResult<RoleDto>> GetPagedRolesAsync(string? search, int page, int pageSize)
        {
            var query = _db.Roles.Where(r => r.IsDeleted != true);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                query = query.Where(r =>
                    (r.Name != null && r.Name.ToLower().Contains(searchTerm)) ||
                    (r.NameMy != null && r.NameMy.ToLower().Contains(searchTerm)) ||
                    (r.Description != null && r.Description.ToLower().Contains(searchTerm)) ||
                    (r.DescriptionMy != null && r.DescriptionMy.ToLower().Contains(searchTerm)));
            }

            return await query
                .OrderBy(r => r.Name)
                .Select(r => new RoleDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    NameMy = r.NameMy,
                    Description = r.Description,
                    DescriptionMy = r.DescriptionMy,
                    CreatedAt = r.CreatedAt ?? DateTime.UtcNow
                })
                .ToPagedResultAsync(page, pageSize);
        }

        public async Task<RoleDto?> GetRoleByIdAsync(long id)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id && r.IsDeleted != true);
            if (role == null)
            {
                return null;
            }

            return new RoleDto
            {
                Id = role.Id,
                Name = role.Name,
                NameMy = role.NameMy,
                Description = role.Description,
                DescriptionMy = role.DescriptionMy,
                CreatedAt = role.CreatedAt ?? DateTime.UtcNow
            };
        }

        public async Task<Result<RoleDto>> CreateRoleAsync(CreateRoleDto dto, long? currentUserId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) && string.IsNullOrWhiteSpace(dto.NameMy))
            {
                return Result<RoleDto>.Failure(ResultMessages.RoleNameRequired, 400);
            }

            var nameExists = !string.IsNullOrWhiteSpace(dto.Name) && await _db.Roles.AnyAsync(r => r.Name == dto.Name);
            if (nameExists)
            {
                return Result<RoleDto>.Failure("Role name is already taken.", 400);
            }

            if (dto.AccessCodes != null && dto.AccessCodes.Any())
            {
                var validCodesResult = await ValidateAccessCodesAsync(dto.AccessCodes);
                if (!validCodesResult.IsSuccess)
                {
                    return Result<RoleDto>.Failure(validCodesResult.ErrorMessage ?? ResultMessages.FailedToCreateRole, validCodesResult.StatusCode);
                }
            }

            var role = new RoleEntity
            {
                Name = dto.Name ?? string.Empty,
                NameMy = dto.NameMy,
                Description = dto.Description,
                DescriptionMy = dto.DescriptionMy,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserId
            };

            _db.Roles.Add(role);
            await _db.SaveChangesAsync();

            await _auditLog.LogAsync("Create", "Role", $"Created new role '{role.Name}'");

            var assignResult = await AssignAccessToRoleAsync(
                role.Id,
                new AssignAccessDto { AccessCodes = dto.AccessCodes ?? new List<string>() },
                currentUserId);
            if (!assignResult.IsSuccess)
            {
                return Result<RoleDto>.Failure(assignResult.ErrorMessage ?? ResultMessages.FailedToCreateRole, assignResult.StatusCode);
            }

            return Result<RoleDto>.Success(new RoleDto
            {
                Id = role.Id,
                Name = role.Name,
                NameMy = role.NameMy,
                Description = role.Description,
                DescriptionMy = role.DescriptionMy,
                CreatedAt = role.CreatedAt ?? DateTime.UtcNow
            }, 201);
        }

        public async Task<Result> UpdateRoleAsync(long id, UpdateRoleDto dto, long? currentUserId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) && string.IsNullOrWhiteSpace(dto.NameMy))
            {
                return Result.Failure(ResultMessages.RoleNameRequired, 400);
            }

            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id && r.IsDeleted != true);
            if (role == null)
            {
                return Result.Failure(ResultMessages.RoleNotFound(id), 404);
            }

            var nameExists = !string.IsNullOrWhiteSpace(dto.Name) && await _db.Roles.AnyAsync(r => r.Name == dto.Name && r.Id != id && r.IsDeleted != true);
            if (nameExists)
            {
                return Result.Failure("Role name is already taken by another role.", 400);
            }

            role.Name = dto.Name ?? string.Empty;
            role.NameMy = dto.NameMy;
            role.Description = dto.Description;
            role.DescriptionMy = dto.DescriptionMy;
            role.UpdatedAt = DateTime.UtcNow;
            role.UpdatedBy = currentUserId;

            _db.Roles.Update(role);
            await _db.SaveChangesAsync();
            await _auditLog.LogAsync("Update", "Role", $"Updated role name/description for '{role.Name}'");
            return Result.Success(200);
        }

        public async Task<Result> SoftDeleteRoleAsync(long id, long? currentUserId = null)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id && r.IsDeleted != true);
            if (role == null)
            {
                return Result.Failure(ResultMessages.RoleNotFound(id), 404);
            }

            var usersCount = await _db.Users.CountAsync(u => u.RoleId == id && !u.IsDeleted);
            if (usersCount > 0)
            {
                return Result.Failure($"you can't delete this role because there are {usersCount} users in this role.", 400);
            }

            role.IsDeleted = true;
            role.UpdatedAt = DateTime.UtcNow;
            role.UpdatedBy = currentUserId;
            _db.Roles.Update(role);
            await _db.SaveChangesAsync();
            await _auditLog.LogAsync("Delete", "Role", $"Deleted role '{role.Name}'");
            return Result.Success(200);
        }

        public async Task<Result<List<string>>> GetAssignedAccessCodesByRoleIdAsync(long roleId)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.IsDeleted != true);
            if (role == null)
            {
                return Result<List<string>>.Failure(ResultMessages.RoleNotFound(roleId), 404);
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
                .Where(rm => rm.RoleId == role.Id && !rm.IsDeleted)
                .Select(rm => rm.Menu.MenuCode)
                .ToListAsync();

            var permissionCodes = await _db.RolePermissions
                .Where(rp => rp.RoleId == role.Id && !rp.IsDeleted)
                .Select(rp => rp.Permission.PermissionCode)
                .ToListAsync();

            var assignedCodes = menuCodes
                .Concat(permissionCodes)
                .Where(code => validCodes.Contains(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Result<List<string>>.Success(assignedCodes);
        }

        public async Task<Result> AssignAccessToRoleAsync(long roleId, AssignAccessDto dto, long? currentUserId = null)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.IsDeleted != true);
            if (role == null)
            {
                return Result.Failure(ResultMessages.RoleNotFound(roleId), 404);
            }

            if (dto.AccessCodes == null)
            {
                return Result.Failure(ResultMessages.AccessCodesCannotBeNull, 400);
            }

            var selectedCodes = dto.AccessCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var validCodesResult = await ValidateAccessCodesAsync(selectedCodes);
            if (!validCodesResult.IsSuccess)
            {
                return Result.Failure(validCodesResult.ErrorMessage ?? ResultMessages.FailedToUpdateRole, validCodesResult.StatusCode);
            }

            var selectedMenus = await _db.Menus
                .Where(m => !m.IsDeleted && selectedCodes.Contains(m.MenuCode))
                .Select(m => new { m.MenuId, m.MenuCode, m.ParentMenuId })
                .ToListAsync();

            var selectedPermissions = await _db.Permissions
                .Where(p => !p.IsDeleted && selectedCodes.Contains(p.PermissionCode))
                .Select(p => new { p.PermissionId, p.PermissionCode, p.MenuId })
                .ToListAsync();

            var menuLookup = await _db.Menus
                .Where(m => !m.IsDeleted)
                .Select(m => new { m.MenuId, m.ParentMenuId })
                .ToListAsync();

            var menuIdLookup = menuLookup.ToDictionary(x => x.MenuId, x => x.ParentMenuId);
            var menuIdsToPersist = new HashSet<long>();
            var now = DateTime.UtcNow;

            foreach (var menu in selectedMenus)
            {
                AddWithAncestors(menu.MenuId, menuIdLookup, menuIdsToPersist);
            }

            foreach (var permission in selectedPermissions)
            {
                AddWithAncestors(permission.MenuId, menuIdLookup, menuIdsToPersist);
            }

            var permissionIdsToPersist = selectedPermissions
                .Select(p => p.PermissionId)
                .Distinct()
                .ToList();

            var existingRoleMenus = await _db.RoleMenus
                .Where(rm => rm.RoleId == role.Id)
                .OrderBy(rm => rm.RoleMenuId)
                .ToListAsync();
            var existingRolePermissions = await _db.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .OrderBy(rp => rp.RolePermissionId)
                .ToListAsync();

            foreach (var duplicateMenuGroup in existingRoleMenus.GroupBy(rm => rm.MenuId).Where(g => g.Count() > 1))
            {
                foreach (var duplicate in duplicateMenuGroup.Skip(1))
                {
                    duplicate.IsDeleted = true;
                    duplicate.UpdatedAt = now;
                    duplicate.UpdatedById = currentUserId;
                }
            }

            foreach (var duplicatePermissionGroup in existingRolePermissions.GroupBy(rp => rp.PermissionId).Where(g => g.Count() > 1))
            {
                foreach (var duplicate in duplicatePermissionGroup.Skip(1))
                {
                    duplicate.IsDeleted = true;
                    duplicate.UpdatedAt = now;
                    duplicate.UpdatedById = currentUserId;
                }
            }

            var oldMenuIds = existingRoleMenus
                .Where(rm => !rm.IsDeleted)
                .Select(rm => rm.MenuId)
                .ToHashSet();
            var oldPermissionIds = existingRolePermissions
                .Where(rp => !rp.IsDeleted)
                .Select(rp => rp.PermissionId)
                .ToHashSet();

            var existingRoleMenusByMenuId = existingRoleMenus
                .GroupBy(rm => rm.MenuId)
                .ToDictionary(g => g.Key, g => g.First());
            var existingRolePermissionsByPermissionId = existingRolePermissions
                .GroupBy(rp => rp.PermissionId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var existingMenu in existingRoleMenus)
            {
                var shouldBeActive = menuIdsToPersist.Contains(existingMenu.MenuId);

                if (shouldBeActive)
                {
                    if (existingMenu.IsDeleted)
                    {
                        existingMenu.IsDeleted = false;
                        existingMenu.UpdatedAt = now;
                        existingMenu.UpdatedById = currentUserId;
                    }
                }
                else if (!existingMenu.IsDeleted)
                {
                    existingMenu.IsDeleted = true;
                    existingMenu.UpdatedAt = now;
                    existingMenu.UpdatedById = currentUserId;
                }
            }

            foreach (var menuId in menuIdsToPersist)
            {
                if (existingRoleMenusByMenuId.ContainsKey(menuId))
                {
                    continue;
                }

                _db.RoleMenus.Add(new RoleMenu
                {
                    RoleId = role.Id,
                    MenuId = menuId,
                    IsDeleted = false,
                    CreatedAt = now,
                    CreatedById = currentUserId
                });
            }

            foreach (var existingPermission in existingRolePermissions)
            {
                var shouldBeActive = permissionIdsToPersist.Contains(existingPermission.PermissionId);

                if (shouldBeActive)
                {
                    if (existingPermission.IsDeleted)
                    {
                        existingPermission.IsDeleted = false;
                        existingPermission.UpdatedAt = now;
                        existingPermission.UpdatedById = currentUserId;
                    }
                }
                else if (!existingPermission.IsDeleted)
                {
                    existingPermission.IsDeleted = true;
                    existingPermission.UpdatedAt = now;
                    existingPermission.UpdatedById = currentUserId;
                }
            }

            foreach (var permissionId in permissionIdsToPersist)
            {
                if (existingRolePermissionsByPermissionId.ContainsKey(permissionId))
                {
                    continue;
                }

                _db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permissionId,
                    IsDeleted = false,
                    CreatedAt = now,
                    CreatedById = currentUserId
                });
            }

            // Build a human-readable diff of what changed
            // Load all menu and permission names for diff display
            var allMenuNames = await _db.Menus
                .Where(m => !m.IsDeleted)
                .Select(m => new { m.MenuId, m.MenuName, m.MenuCode })
                .ToListAsync();
            var allPermNames = await _db.Permissions
                .Include(p => p.Menu)
                .Where(p => !p.IsDeleted)
                .Select(p => new { p.PermissionId, p.PermissionCode, p.ActionName, MenuName = p.Menu.MenuName })
                .ToListAsync();

            var menuNameLookup = allMenuNames.ToDictionary(m => m.MenuId, m => m.MenuName);
            var permNameLookup = allPermNames.ToDictionary(p => p.PermissionId,
                p => $"{p.MenuName} — {p.ActionName}");

            var addedMenuNames = menuIdsToPersist
                .Except(oldMenuIds)
                .Select(id => menuNameLookup.TryGetValue(id, out var n) ? n : id.ToString())
                .OrderBy(n => n)
                .ToList();

            var removedMenuNames = oldMenuIds
                .Except(menuIdsToPersist)
                .Select(id => menuNameLookup.TryGetValue(id, out var n) ? n : id.ToString())
                .OrderBy(n => n)
                .ToList();

            var addedPermNames = permissionIdsToPersist
                .Except(oldPermissionIds)
                .Select(id => permNameLookup.TryGetValue(id, out var n) ? n : id.ToString())
                .OrderBy(n => n)
                .ToList();

            var removedPermNames = oldPermissionIds
                .Except(new HashSet<long>(permissionIdsToPersist))
                .Select(id => permNameLookup.TryGetValue(id, out var n) ? n : id.ToString())
                .OrderBy(n => n)
                .ToList();

            var descParts = new System.Text.StringBuilder();
            descParts.Append($"Updated access permissions for role '{role.Name}'.");

            if (addedMenuNames.Any() || addedPermNames.Any())
            {
                var added = addedMenuNames.Concat(addedPermNames).OrderBy(n => n).ToList();
                descParts.Append($" Granted: {string.Join(", ", added)}.");
            }

            if (removedMenuNames.Any() || removedPermNames.Any())
            {
                var removed = removedMenuNames.Concat(removedPermNames).OrderBy(n => n).ToList();
                descParts.Append($" Revoked: {string.Join(", ", removed)}.");
            }

            if (!addedMenuNames.Any() && !addedPermNames.Any() && !removedMenuNames.Any() && !removedPermNames.Any())
            {
                descParts.Append(" No changes detected.");
            }

            await _db.SaveChangesAsync();
            await _auditLog.LogAsync("AssignAccess", "Role", descParts.ToString());
            return Result.Success(200);
        }

        private static void AddWithAncestors(long menuId, IReadOnlyDictionary<long, long?> parentLookup, ISet<long> target)
        {
            var current = menuId;

            while (current > 0 && target.Add(current))
            {
                if (!parentLookup.TryGetValue(current, out var parentId) || !parentId.HasValue)
                {
                    break;
                }

                current = parentId.Value;
            }
        }

        private async Task<Result> ValidateAccessCodesAsync(IEnumerable<string> codes)
        {
            var normalizedCodes = codes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim())
                .ToList();

            var validAccessCodes = await _db.Menus
                .Where(m => !m.IsDeleted)
                .Select(m => m.MenuCode)
                .ToListAsync();

            var validPermissionCodes = await _db.Permissions
                .Where(p => !p.IsDeleted)
                .Select(p => p.PermissionCode)
                .ToListAsync();

            var validCodes = validAccessCodes
                .Concat(validPermissionCodes)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var invalidCodes = normalizedCodes
                .Where(code => !validCodes.Contains(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (invalidCodes.Any())
            {
                return Result.Failure(ResultMessages.InvalidAccessCodes(string.Join(", ", invalidCodes)), 400);
            }

            return Result.Success(200);
        }
    }
}



