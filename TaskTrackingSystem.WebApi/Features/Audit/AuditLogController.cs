using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.AuditLog;
using TaskTrackingSystem.WebApi.Infrastructure;

namespace TaskTrackingSystem.WebApi.Features.Audit
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AuditLogController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly PermissionAuthorizationService _permissionAuthorizationService;

        public AuditLogController(AppDbContext db, PermissionAuthorizationService permissionAuthorizationService)
        {
            _db = db;
            _permissionAuthorizationService = permissionAuthorizationService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> GetAuditLogs(
            [FromQuery] string? search,
            [FromQuery] PaginationQuery? paging = null)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/AuditLog", "List"))
            {
                return Forbid();
            }

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

            if (paging == null || (!paging.Page.HasValue && !paging.Limit.HasValue))
            {
                var fullLogs = await query
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

                return Ok(fullLogs);
            }

            var page = PaginationExtensions.NormalizePage(paging.Page);
            var limit = PaginationExtensions.NormalizePageSize(paging.Limit ?? 0);

            var pagedLogs = await query
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
                .ToPagedResultAsync(page, limit);

            return Ok(pagedLogs);
        }
    }
}
