using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Auth;
using TaskTrackingSystem.Database;
using TaskTrackingSystem.Database.AppDbContextModels;

namespace TaskTrackingSystem.WebApi.Features.Auth
{
    public class AuthService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly IPasswordHasher<TaskTrackingSystem.Database.AppDbContextModels.User> _passwordHasher;

        public AuthService(AppDbContext db, IConfiguration configuration, IPasswordHasher<TaskTrackingSystem.Database.AppDbContextModels.User> passwordHasher)
        {
            _db = db;
            _configuration = configuration;
            _passwordHasher = passwordHasher;
        }

        public async Task<AuthResponseDto?> LoginAsync(LoginDto loginDto)
        {
            
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => (u.Username == loginDto.UsernameOrEmail || u.Email == loginDto.UsernameOrEmail)
                                           && !u.IsDeleted);

            if (user == null) return null;

            
            if (!user.IsActive)
            {
                throw new InvalidOperationException("This account has been deactivated.");
            }

            
            var passwordCheck = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, loginDto.Password);
            if (passwordCheck == PasswordVerificationResult.Failed)
            {
                if (!IsLegacyPasswordMatch(user.PasswordHash, loginDto.Password))
                {
                    return null;
                }

                user.PasswordHash = _passwordHasher.HashPassword(user, loginDto.Password);
                await _db.SaveChangesAsync();
            }

            var roleId = user.RoleId;
            if (roleId <= 0 && user.Role != null)
            {
                roleId = user.Role.Id;
            }

            var roleName = user.Role?.Name ?? string.Empty;
            var token = GenerateJwtToken(user.Id.ToString(), user.Username, user.Email, roleId, roleName);

            return new AuthResponseDto
            {
                Token = token,
                Username = user.Username,
                Email = user.Email,
                RoleId = roleId,
                RoleName = roleName
            };
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            var usernameExists = await _db.Users.AnyAsync(u => u.Username == dto.Username && !u.IsDeleted);
            if (usernameExists)
            {
                throw new InvalidOperationException("Username is already taken.");
            }

            var emailExists = await _db.Users.AnyAsync(u => u.Email == dto.Email && !u.IsDeleted);
            if (emailExists)
            {
                throw new InvalidOperationException("Email is already registered.");
            }

            // Find default Role (Employee role, or fallback to lowest ID)
            var defaultRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Employee" && r.IsDeleted != true)
                              ?? await _db.Roles.FirstOrDefaultAsync(r => r.IsDeleted != true);
            if (defaultRole == null)
            {
                throw new InvalidOperationException("No system roles configured. Registration failed.");
            }

            var user = new TaskTrackingSystem.Database.AppDbContextModels.User
            {
                Username = dto.Username,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                PasswordHash = string.Empty,
                Phone = dto.Phone,
                RoleId = defaultRole.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var token = GenerateJwtToken(user.Id.ToString(), user.Username, user.Email, defaultRole.Id, defaultRole.Name);

            return new AuthResponseDto
            {
                Token = token,
                Username = user.Username,
                Email = user.Email,
                RoleId = defaultRole.Id,
                RoleName = defaultRole.Name
            };
        }

        public async Task<Result> ResetPasswordAsync(ResetPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.UsernameOrEmail) ||
                string.IsNullOrWhiteSpace(dto.RecoveryCode) ||
                string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                return Result.Failure(ResultMessages.FillAllFields, 400);
            }

            var expectedCode = _configuration["PasswordReset:RecoveryCode"] ?? "TASKIFY-RESET-2026";
            if (!string.Equals(dto.RecoveryCode.Trim(), expectedCode, StringComparison.Ordinal))
            {
                return Result.Failure("Invalid recovery code.", 400);
            }

            var user = await _db.Users
                .FirstOrDefaultAsync(u => (u.Username == dto.UsernameOrEmail || u.Email == dto.UsernameOrEmail) && !u.IsDeleted);

            if (user == null)
            {
                return Result.Failure("Account not found.", 404);
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            return Result.Success(200);
        }

        private string GenerateJwtToken(string userId, string username, string email, long roleId, string roleName)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var keyStr = jwtSettings["Key"] ?? "SUPER_SECRET_KEY_THAT_IS_LONG_ENOUGH_12345";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Email, email),
                new Claim("role_id", roleId.ToString()),
                new Claim(ClaimTypes.Role, roleName)
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"] ?? "YourApp",
                audience: jwtSettings["Audience"] ?? "YourAppUsers",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static bool IsLegacyPasswordMatch(string passwordHash, string password)
        {
            return string.Equals(passwordHash, "HASHED_" + password, StringComparison.Ordinal);
        }
    }
}
