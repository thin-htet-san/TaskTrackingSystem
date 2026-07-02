using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.User;
using TaskTrackingSystem.WebApi.Infrastructure;

namespace TaskTrackingSystem.WebApi.Features.User
{
    public class UserService
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<TaskTrackingSystem.Database.AppDbContextModels.User> _passwordHasher;
        private readonly Infrastructure.AuditLogService _auditLog;

        public UserService(AppDbContext db, IPasswordHasher<TaskTrackingSystem.Database.AppDbContextModels.User> passwordHasher, Infrastructure.AuditLogService auditLog)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _auditLog = auditLog;
        }

        public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
        {
            return await _db.Users
                .Where(u => !u.IsDeleted)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Phone = u.Phone,
                    RoleId = u.RoleId,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt ?? DateTime.UtcNow
                }).ToListAsync();
        }

        public async Task<PagedResult<UserDto>> GetPagedUsersAsync(string? search, long? roleId, string? status, int page, int pageSize)
        {
            var query = _db.Users.Where(u => !u.IsDeleted);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                query = query.Where(u =>
                    (u.FirstName != null && u.FirstName.ToLower().Contains(searchTerm)) ||
                    (u.LastName != null && u.LastName.ToLower().Contains(searchTerm)) ||
                    (u.Username != null && u.Username.ToLower().Contains(searchTerm)) ||
                    (u.Email != null && u.Email.ToLower().Contains(searchTerm)));
            }

            if (roleId.HasValue && roleId.Value > 0)
            {
                query = query.Where(u => u.RoleId == roleId.Value);
            }

            if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => u.IsActive);
            }
            else if (string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => !u.IsActive);
            }

            return await query
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ThenBy(u => u.Username)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Phone = u.Phone,
                    RoleId = u.RoleId,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt ?? DateTime.UtcNow
                })
                .ToPagedResultAsync(page, pageSize);
        }

        public async Task<UserDto?> GetUserByIdAsync(long id)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

            if (user == null) return null;

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username, 
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                RoleId = user.RoleId,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt ?? DateTime.UtcNow
            };
        }

        public async Task<Result<UserDto>> CreateUserAsync(CreateUserDto dto, long? currentUserId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName) || string.IsNullOrWhiteSpace(dto.Email))
            {
                return Result<UserDto>.Failure(ResultMessages.FillAllFields, 400);
            }

            var usernameExists = await _db.Users.AnyAsync(u => u.Username == dto.Username && !u.IsDeleted);
            if (usernameExists)
            {
                return Result<UserDto>.Failure("Username is already taken.", 400);
            }

            var emailExists = await _db.Users.AnyAsync(u => u.Email == dto.Email && !u.IsDeleted);
            if (emailExists)
            {
                return Result<UserDto>.Failure("Email is already registered.", 400);
            }

            var user = new TaskTrackingSystem.Database.AppDbContextModels.User
            {
                Username = dto.Username, 
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                PasswordHash = string.Empty,
                Phone = dto.Phone,
                RoleId = dto.RoleId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserId
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var roleName = await _db.Roles.Where(r => r.Id == user.RoleId).Select(r => r.Name).FirstOrDefaultAsync() ?? "Unknown";
            await _auditLog.LogAsync("Create", "User", $"Created user '{user.Username}' ({user.FirstName} {user.LastName}, {user.Email}) with role '{roleName}'");

            var resultDto = new UserDto
            {
                Id = user.Id,
                Username = user.Username, 
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                RoleId = user.RoleId,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt ?? DateTime.UtcNow
            };

            return Result<UserDto>.Success(resultDto, 201);
        }

        public async Task<Result> UpdateUserAsync(long id, UpdateUserDto dto, long? currentUserId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
            {
                return Result.Failure(ResultMessages.FillAllFields, 400);
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
            if (user == null) return Result.Failure(ResultMessages.UserNotFound(id), 404);

            var usernameExists = await _db.Users.AnyAsync(u => u.Username == dto.Username && u.Id != id && !u.IsDeleted);
            if (usernameExists)
            {
                return Result.Failure("Username is already taken by another user.", 400);
            }

            user.Username = dto.Username; 
            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.Phone = dto.Phone;
            user.RoleId = dto.RoleId;
            user.IsActive = dto.IsActive;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = currentUserId;

            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            var roleName = await _db.Roles.Where(r => r.Id == user.RoleId).Select(r => r.Name).FirstOrDefaultAsync() ?? "Unknown";
            var statusLabel = user.IsActive ? "Active" : "Inactive";
            await _auditLog.LogAsync("Update", "User", $"Updated user '{user.Username}' ({user.FirstName} {user.LastName}) — Role: '{roleName}', Status: {statusLabel}");

            return Result.Success(200);
        }

        public async Task<Result> SoftDeleteUserAsync(long id, long? loggedInUserId = null)
        {
            if (loggedInUserId.HasValue && loggedInUserId.Value == id)
            {
                return Result.Failure("You cannot delete your own account.", 400);
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
            if (user == null) return Result.Failure(ResultMessages.UserNotFound(id), 404);

            user.IsDeleted = true;
            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            await _auditLog.LogAsync("Delete", "User", $"Deleted user account '{user.Username}' ({user.FirstName} {user.LastName})");

            return Result.Success(200);
        }

        public async Task<List<long>> GetMyProjectIdsAsync(long currentUserId)
        {
            return await _db.ProjectMembers
                .Where(pm => pm.UserId == currentUserId)
                .Select(pm => pm.ProjectId)
                .Distinct()
                .ToListAsync();
        }

        public async Task<List<long>> GetTeamUserIdsAsync(List<long> projectIds, long currentUserId)
        {
            if (projectIds == null || projectIds.Count == 0)
                return new List<long>();

            return await _db.ProjectMembers
                .Where(pm => projectIds.Contains(pm.ProjectId) && pm.UserId != currentUserId)
                .Select(pm => pm.UserId)
                .Distinct()
                .ToListAsync();
        }
    }
}
