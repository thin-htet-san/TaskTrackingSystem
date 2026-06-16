using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Role;

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
                }).ToListAsync();
        }

        public async Task<RoleDto?> GetRoleByIdAsync(long id)
        {
            var role = await _db.Roles
                .FirstOrDefaultAsync(r => r.Id == id && r.IsDeleted != true);

            if (role == null) return null;

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

            var role = new TaskTrackingSystem.Database.AppDbContextModels.Role
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

            var resultDto = new RoleDto
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                CreatedAt = role.CreatedAt ?? DateTime.UtcNow
            };

            return Result<RoleDto>.Success(resultDto, 201);
        }

        public async Task<Result> UpdateRoleAsync(long id, UpdateRoleDto dto, long? currentUserId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Result.Failure(ResultMessages.RoleNameRequired, 400);
            }

            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id && r.IsDeleted != true);
            if (role == null) return Result.Failure(ResultMessages.RoleNotFound(id), 404);

            var nameExists = await _db.Roles.AnyAsync(r => r.Name == dto.Name && r.Id != id && r.IsDeleted != true);
            if (nameExists)
            {
                return Result.Failure("Role name is already taken by another role.", 400);
            }

            var oldName = role.Name;
            role.Name = dto.Name;
            role.Description = dto.Description;
            role.UpdatedAt = DateTime.UtcNow;
            role.UpdatedBy = currentUserId;

            var relatedRoleMenus = await _db.RoleMenus
                .Where(rm => rm.RoleId == role.Id || rm.RoleCode == oldName)
                .ToListAsync();

            foreach (var roleMenu in relatedRoleMenus)
            {
                roleMenu.RoleCode = dto.Name;
            }

            _db.Roles.Update(role);
            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        public async Task<Result> SoftDeleteRoleAsync(long id)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id && r.IsDeleted != true);
            if (role == null) return Result.Failure(ResultMessages.RoleNotFound(id), 404);

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
                .Where(rm => rm.RoleId == role.Id && rm.DelFlag == 0)
                .Select(rm => rm.MenuCode)
                .ToListAsync();

            if (!menuCodes.Any())
            {
                menuCodes = await _db.RoleMenus
                    .Where(rm => rm.RoleCode == role.Name && rm.DelFlag == 0)
                    .Select(rm => rm.MenuCode)
                    .ToListAsync();
            }

            return Result<List<string>>.Success(menuCodes);
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

            var validCodesResult = await ValidatePermissionCodesAsync(dto.MenuCodes);
            if (!validCodesResult.IsSuccess)
            {
                return Result.Failure(validCodesResult.ErrorMessage ?? ResultMessages.FailedToUpdateRole, validCodesResult.StatusCode);
            }

            // Remove existing role menus matching either the new RoleId link or the old RoleCode link.
            var existingRoleMenus = await _db.RoleMenus
                .Where(rm => rm.RoleId == role.Id || rm.RoleCode == role.Name)
                .ToListAsync();

            if (existingRoleMenus.Any())
            {
                _db.RoleMenus.RemoveRange(existingRoleMenus);
            }

            // Bulk insert new RoleMenu links
            int i = 1;
            foreach (var menuCode in dto.MenuCodes)
            {
                _db.RoleMenus.Add(new RoleMenu
                {
                    RoleMenuId = $"RM_{role.Id}_{i++}_{DateTime.UtcNow.Ticks}",
                    RoleId = role.Id,
                    RoleCode = role.Name,
                    MenuCode = menuCode,
                    DelFlag = 0,
                    CreatedDateTime = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        private async Task<Result> ValidatePermissionCodesAsync(IEnumerable<string> menuCodes)
        {
            var validMenuCodes = await _db.MenuAdmins
                .Where(m => m.DelFlag == 0)
                .Select(m => m.MenuCode)
                .ToListAsync();

            var validActionCodes = await _db.MenuAdminDetails
                .Where(d => d.DelFlag == 0)
                .Select(d => d.MenuDetailCode)
                .ToListAsync();

            var validCodes = validMenuCodes
                .Concat(validActionCodes)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var invalidCodes = menuCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
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
