using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Role;
using RoleEntity = TaskTrackingSystem.Database.AppDbContextModels.Role;

namespace TaskTrackingSystem.WebApi.Features.Role
{
    public class RoleService
    {
        private readonly AppDbContext _db;

        public RoleService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<RoleDto>> GetAllRolesAsync()
        {
            return await _db.Roles
                .Where(r => r.IsDeleted != true)
                .Select(r => new RoleDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    CreatedAt = r.CreatedAt ?? DateTime.UtcNow
                })
                .ToListAsync();
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
                Description = role.Description,
                CreatedAt = role.CreatedAt ?? DateTime.UtcNow
            };
        }

        public async Task<Result<RoleDto>> CreateRoleAsync(CreateRoleDto dto, long? currentUserId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Result<RoleDto>.Failure(ResultMessages.RoleNameRequired, 400);
            }

            var nameExists = await _db.Roles.AnyAsync(r => r.Name == dto.Name && r.IsDeleted != true);
            if (nameExists)
            {
                return Result<RoleDto>.Failure("Role name is already taken.", 400);
            }

            if (dto.MenuCodes != null && dto.MenuCodes.Any())
            {
                var validCodesResult = await ValidatePermissionCodesAsync(dto.MenuCodes);
                if (!validCodesResult.IsSuccess)
                {
                    return Result<RoleDto>.Failure(validCodesResult.ErrorMessage ?? ResultMessages.FailedToCreateRole, validCodesResult.StatusCode);
                }
            }

            var role = new RoleEntity
            {
                Name = dto.Name,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserId
            };

            _db.Roles.Add(role);
            await _db.SaveChangesAsync();

            var assignResult = await AssignMenusToRoleAsync(role.Id, new AssignMenusDto { MenuCodes = dto.MenuCodes ?? new List<string>() });
            if (!assignResult.IsSuccess)
            {
                return Result<RoleDto>.Failure(assignResult.ErrorMessage ?? ResultMessages.FailedToCreateRole, assignResult.StatusCode);
            }

            return Result<RoleDto>.Success(new RoleDto
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                CreatedAt = role.CreatedAt ?? DateTime.UtcNow
            }, 201);
        }

        public async Task<Result> UpdateRoleAsync(long id, UpdateRoleDto dto, long? currentUserId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Result.Failure(ResultMessages.RoleNameRequired, 400);
            }

            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id && r.IsDeleted != true);
            if (role == null)
            {
                return Result.Failure(ResultMessages.RoleNotFound(id), 404);
            }

            var nameExists = await _db.Roles.AnyAsync(r => r.Name == dto.Name && r.Id != id && r.IsDeleted != true);
            if (nameExists)
            {
                return Result.Failure("Role name is already taken by another role.", 400);
            }

            role.Name = dto.Name;
            role.Description = dto.Description;
            role.UpdatedAt = DateTime.UtcNow;
            role.UpdatedBy = currentUserId;

            _db.Roles.Update(role);
            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        public async Task<Result> SoftDeleteRoleAsync(long id)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id && r.IsDeleted != true);
            if (role == null)
            {
                return Result.Failure(ResultMessages.RoleNotFound(id), 404);
            }

            role.IsDeleted = true;
            _db.Roles.Update(role);
            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        public async Task<Result<List<string>>> GetAssignedMenusByRoleIdAsync(long roleId)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.IsDeleted != true);
            if (role == null)
            {
                return Result<List<string>>.Failure(ResultMessages.RoleNotFound(roleId), 404);
            }

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
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Result<List<string>>.Success(assignedCodes);
        }

        public async Task<Result> AssignMenusToRoleAsync(long roleId, AssignMenusDto dto)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.IsDeleted != true);
            if (role == null)
            {
                return Result.Failure(ResultMessages.RoleNotFound(roleId), 404);
            }

            if (dto.MenuCodes == null)
            {
                return Result.Failure("MenuCodes cannot be null", 400);
            }

            var selectedCodes = dto.MenuCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var validCodesResult = await ValidatePermissionCodesAsync(selectedCodes);
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

            var existingRoleMenus = await _db.RoleMenus.Where(rm => rm.RoleId == role.Id).ToListAsync();
            var existingRolePermissions = await _db.RolePermissions.Where(rp => rp.RoleId == role.Id).ToListAsync();

            if (existingRoleMenus.Any())
            {
                _db.RoleMenus.RemoveRange(existingRoleMenus);
            }

            if (existingRolePermissions.Any())
            {
                _db.RolePermissions.RemoveRange(existingRolePermissions);
            }

            foreach (var menuId in menuIdsToPersist)
            {
                _db.RoleMenus.Add(new RoleMenu
                {
                    RoleId = role.Id,
                    MenuId = menuId,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            foreach (var permissionId in permissionIdsToPersist)
            {
                _db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permissionId,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
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

        private async Task<Result> ValidatePermissionCodesAsync(IEnumerable<string> codes)
        {
            var normalizedCodes = codes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim())
                .ToList();

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

            var invalidCodes = normalizedCodes
                .Where(code => !validCodes.Contains(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (invalidCodes.Any())
            {
                return Result.Failure(ResultMessages.InvalidPermissionIds(string.Join(", ", invalidCodes)), 400);
            }

            return Result.Success(200);
        }
    }
}
