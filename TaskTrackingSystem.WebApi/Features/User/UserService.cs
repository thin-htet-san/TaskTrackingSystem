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
    }
}
