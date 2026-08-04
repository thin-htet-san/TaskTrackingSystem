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
                var searchTerm = search.Trim();
                var searchPattern = $"%{searchTerm}%";
                var normalizedSearch = searchTerm.Replace(" ", string.Empty, StringComparison.Ordinal)
                    .ToLowerInvariant();
                var searchModuleLabel = normalizedSearch is "module" or "အပိုင်း";
                var searchActionLabel = normalizedSearch is "actions" or "လုပ်ဆောင်ချက်များ";
                var searchRoleModule = normalizedSearch.Contains("role", StringComparison.Ordinal) ||
                                       searchTerm.Contains("အခန်းကဏ္ဍ", StringComparison.Ordinal);
                var searchProjectModule = normalizedSearch.Contains("project", StringComparison.Ordinal) ||
                                          searchTerm.Contains("စီမံကိန်း", StringComparison.Ordinal);
                var searchTaskModule = normalizedSearch.Contains("task", StringComparison.Ordinal) ||
                                       searchTerm.Contains("လုပ်ငန်း", StringComparison.Ordinal);
                var searchUserModule = normalizedSearch.Contains("user", StringComparison.Ordinal) ||
                                       searchTerm.Contains("အသုံးပြုသူ", StringComparison.Ordinal);
                var searchIssueModule = normalizedSearch.Contains("issue", StringComparison.Ordinal) ||
                                        searchTerm.Contains("ပြဿနာ", StringComparison.Ordinal);
                var searchAuditModule = normalizedSearch.Contains("audit", StringComparison.Ordinal) ||
                                        searchTerm.Contains("မှတ်တမ်း", StringComparison.Ordinal);
                var searchDashboardModule = normalizedSearch.Contains("dashboard", StringComparison.Ordinal) ||
                                            searchTerm.Contains("ပင်မစာမျက်နှာ", StringComparison.Ordinal);
                var searchReportModule = normalizedSearch.Contains("report", StringComparison.Ordinal) ||
                                         searchTerm.Contains("အစီရင်ခံစာ", StringComparison.Ordinal);
                var searchCreateAction = normalizedSearch is "create" or "created" or "add" or "added" ||
                                         searchTerm.Contains("ဖန်တီး", StringComparison.Ordinal);
                var searchUpdateAction = normalizedSearch is "update" or "updated" or "edit" or "edited" ||
                                         searchTerm.Contains("အပ်ဒိတ်", StringComparison.Ordinal);
                var searchDeleteAction = normalizedSearch is "delete" or "deleted" ||
                                         searchTerm.Contains("ဖျက်", StringComparison.Ordinal);
                var searchAssignAction = normalizedSearch is "assign" or "assigned" or "assignaccess" ||
                                         searchTerm.Contains("ခွဲဝေ", StringComparison.Ordinal);
                query = query.Where(a => 
                    EF.Functions.ILike(a.Action ?? string.Empty, searchPattern) ||
                    EF.Functions.ILike(a.Module ?? string.Empty, searchPattern) ||
                    EF.Functions.ILike(a.Description ?? string.Empty, searchPattern) ||
                    (searchModuleLabel && a.Module != null) ||
                    (searchActionLabel && a.Action != null) ||
                    (searchRoleModule && EF.Functions.ILike(a.Module ?? string.Empty, "%role%")) ||
                    (searchProjectModule && EF.Functions.ILike(a.Module ?? string.Empty, "%project%")) ||
                    (searchTaskModule && EF.Functions.ILike(a.Module ?? string.Empty, "%task%")) ||
                    (searchUserModule && EF.Functions.ILike(a.Module ?? string.Empty, "%user%")) ||
                    (searchIssueModule && EF.Functions.ILike(a.Module ?? string.Empty, "%issue%")) ||
                    (searchAuditModule && EF.Functions.ILike(a.Module ?? string.Empty, "%audit%")) ||
                    (searchDashboardModule && EF.Functions.ILike(a.Module ?? string.Empty, "%dashboard%")) ||
                    (searchReportModule && EF.Functions.ILike(a.Module ?? string.Empty, "%report%")) ||
                    (searchCreateAction && EF.Functions.ILike(a.Action ?? string.Empty, "%create%")) ||
                    (searchUpdateAction && EF.Functions.ILike(a.Action ?? string.Empty, "%update%")) ||
                    (searchDeleteAction && EF.Functions.ILike(a.Action ?? string.Empty, "%delete%")) ||
                    (searchAssignAction && EF.Functions.ILike(a.Action ?? string.Empty, "%assign%")) ||
                    (a.User != null &&
                        (EF.Functions.ILike(a.User.Username ?? string.Empty, searchPattern) ||
                         EF.Functions.ILike(a.User.FirstName ?? string.Empty, searchPattern) ||
                         EF.Functions.ILike(a.User.LastName ?? string.Empty, searchPattern) ||
                         EF.Functions.ILike((a.User.FirstName ?? string.Empty) + " " + (a.User.LastName ?? string.Empty), searchPattern) ||
                         EF.Functions.ILike((a.User.FirstName ?? string.Empty) + (a.User.LastName ?? string.Empty), searchPattern) ||
                         EF.Functions.ILike(a.User.FirstNameMy ?? string.Empty, searchPattern) ||
                         EF.Functions.ILike(a.User.LastNameMy ?? string.Empty, searchPattern) ||
                         EF.Functions.ILike((a.User.FirstNameMy ?? string.Empty) + " " + (a.User.LastNameMy ?? string.Empty), searchPattern) ||
                         EF.Functions.ILike((a.User.FirstNameMy ?? string.Empty) + (a.User.LastNameMy ?? string.Empty), searchPattern))) ||
                    EF.Functions.ILike(a.IpAddress ?? string.Empty, searchPattern)
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
                        UserFullNameMy = a.User != null ? $"{a.User.FirstNameMy} {a.User.LastNameMy}" : null,
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
                    UserFullNameMy = a.User != null ? $"{a.User.FirstNameMy} {a.User.LastNameMy}" : null,
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
