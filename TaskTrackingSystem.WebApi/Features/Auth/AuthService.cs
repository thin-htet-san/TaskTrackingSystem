using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TaskTrackingSystem.Shared.Models.Auth;
using TaskTrackingSystem.Database;
using TaskTrackingSystem.Database.AppDbContextModels;

namespace TaskTrackingSystem.WebApi.Features.Auth
{
    public class AuthService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _configuration;

        public AuthService(AppDbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
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

            
            string inputHash = "HASHED_" + loginDto.Password;
            if (user.PasswordHash != inputHash)
            {
                return null;
            }

            
            var token = GenerateJwtToken(user.Id.ToString(), user.Username, user.Email, user.Role.Name);

            return new AuthResponseDto
            {
                Token = token,
                Username = user.Username,
                Email = user.Email,
                RoleName = user.Role.Name
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

            string fakePasswordHash = "HASHED_" + dto.Password;

            var user = new TaskTrackingSystem.Database.AppDbContextModels.User
            {
                Username = dto.Username,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                PasswordHash = fakePasswordHash,
                Phone = dto.Phone,
                RoleId = defaultRole.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var token = GenerateJwtToken(user.Id.ToString(), user.Username, user.Email, defaultRole.Name);

            return new AuthResponseDto
            {
                Token = token,
                Username = user.Username,
                Email = user.Email,
                RoleName = defaultRole.Name
            };
        }

        private string GenerateJwtToken(string userId, string username, string email, string roleName)
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
    }
}