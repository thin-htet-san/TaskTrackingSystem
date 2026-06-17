using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using TaskTrackingSystem.Database.AppDbContextModels;

namespace TaskTrackingSystem.WebApi.Infrastructure
{
    public class AuditLogService
    {
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        public async System.Threading.Tasks.Task LogAsync(string action, string module, string description)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                long? userId = null;
                string? ipAddress = null;

                if (httpContext != null)
                {
                    var user = httpContext.User;
                    if (user != null)
                    {
                        var id = user.GetUserId();
                        if (id > 0)
                        {
                            userId = id;
                        }
                    }

                    // Get remote IP address
                    ipAddress = httpContext.Connection?.RemoteIpAddress?.ToString();

                    // Fallback to check X-Forwarded-For if behind reverse proxy
                    if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
                    {
                        ipAddress = forwardedFor.ToString().Split(',')[0].Trim();
                    }
                }

                var log = new AuditLog
                {
                    UserId = userId,
                    Action = action,
                    Module = module,
                    Description = description,
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.UtcNow
                };

                _db.AuditLogs.Add(log);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Never let audit logging failure block critical operations
                Console.WriteLine($"[AuditLog Error] Failed to write audit log: {ex.Message}");
            }
        }
    }
}
