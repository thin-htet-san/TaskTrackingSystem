using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared.Models.AuditLog;

namespace TaskTrackingSystem.WebApi.Features.Audit
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AuditLogController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AuditLogController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AuditLogDto>>> GetAuditLogs([FromQuery] string? search)
        {
            var query = _db.AuditLogs
                .Include(a => a.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLower();
                query = query.Where(a => 
                    a.Action.ToLower().Contains(searchLower) ||
                    a.Module.ToLower().Contains(searchLower) ||
                    a.Description.ToLower().Contains(searchLower) ||
                    (a.User != null && (a.User.Username.ToLower().Contains(searchLower) || 
                                       (a.User.FirstName + " " + a.User.LastName).ToLower().Contains(searchLower))) ||
                    (a.IpAddress != null && a.IpAddress.ToLower().Contains(searchLower))
                );
            }

            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AuditLogDto
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    Username = a.User != null ? a.User.Username : "System",
                    UserFullName = a.User != null ? $"{a.User.FirstName} {a.User.LastName}" : "System",
                    Action = a.Action,
                    Module = a.Module,
                    Description = a.Description,
                    IpAddress = a.IpAddress,
                    CreatedAt = a.CreatedAt ?? System.DateTime.UtcNow
                })
                .ToListAsync();

            return Ok(logs);
        }
    }
}
