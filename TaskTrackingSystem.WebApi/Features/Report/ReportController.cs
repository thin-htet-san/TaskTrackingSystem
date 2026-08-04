using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Enums;
using TaskTrackingSystem.Shared.Models.Report;
using TaskTrackingSystem.Shared.Models.Issue;
using TaskTrackingSystem.WebApi.Infrastructure;

namespace TaskTrackingSystem.WebApi.Features.Report
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ReportController : ControllerBase
    {
        private readonly ReportService _reportService;

        public ReportController(ReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("issues")]
        [ProducesResponseType(typeof(List<IssueDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<Result<List<IssueDto>>>> GetIssuesReport()
        {
            var result = await _reportService.GetIssuesReportAsync(User.GetRoleId(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("tasks")]
        [ProducesResponseType(typeof(PagedResult<TaskReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<PagedResult<TaskReportDto>>> GetTasksReport(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] int? projectId,
            [FromQuery] bool? assignedToMe,
            [FromQuery] bool? assignedToMyTeam,
            [FromQuery] PaginationQuery? paging = null)
        {
            (startDate, endDate) = NormalizeDateRange(startDate, endDate);
            if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
            {
                return BadRequest(Result.Failure("End date cannot be earlier than start date.", 400));
            }

            if (paging == null || (!paging.Page.HasValue && !paging.Limit.HasValue))
            {
                var full = await _reportService.GetTasksReportAsync(
                    startDate, endDate, status, search, projectId, User.GetRoleId(), User.GetUserId(), assignedToMe, assignedToMyTeam);
                return StatusCode(full.StatusCode, full);
            }

            var page = PaginationExtensions.NormalizePage(paging.Page);
            var limit = PaginationExtensions.NormalizePageSize(paging.Limit ?? 0);
            var result = await _reportService.GetPagedTasksReportAsync(
                startDate, endDate, status, search, projectId, User.GetRoleId(), User.GetUserId(), page, limit, assignedToMe, assignedToMyTeam);
            return Ok(result);
        }

        [HttpGet("user-productivity")]
        [ProducesResponseType(typeof(PagedResult<UserProductivityDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<PagedResult<UserProductivityDto>>> GetUserProductivityReport(
            [FromQuery] PaginationQuery? paging = null)
        {
            var users = await _reportService.GetUserProductivityReportAsync(User.GetRoleId(), User.GetUserId());
            if (!users.IsSuccess || users.Value == null)
            {
                return StatusCode(users.StatusCode, new PagedResult<UserProductivityDto>());
            }

            if (paging == null || (!paging.Page.HasValue && !paging.Limit.HasValue))
            {
                return StatusCode(users.StatusCode, users);
            }

            var page = PaginationExtensions.NormalizePage(paging?.Page);
            var limit = PaginationExtensions.NormalizePageSize(paging?.Limit ?? 0);
            var query = users.Value.AsQueryable();
            var paged = await query.ToPagedResultAsync(page, limit);
            return Ok(paged);
        }

        [HttpGet("task-status-summary")]
        public async Task<ActionResult<Result<IEnumerable<TaskStatusSummaryDto>>>> GetTaskStatusSummary(
            [FromQuery] string? search,
            [FromQuery] AppTaskStatus? statusId,
            [FromQuery] long? projectId)
        {
            var result = await _reportService.GetTaskStatusSummaryAsync(search, statusId, projectId, User.GetRoleId(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("task-status-summary/excel")]
        public async Task<IActionResult> DownloadTaskStatusSummaryExcel(
            [FromQuery] string? search,
            [FromQuery] AppTaskStatus? statusId,
            [FromQuery] long? projectId)
        {
            var result = await _reportService.GetTaskStatusSummaryAsync(search, statusId, projectId, User.GetRoleId(), User.GetUserId());
            if (!result.IsSuccess || result.Value == null)
                return BadRequest(new { message = result.ErrorMessage });
            var bytes = _reportService.ExportTaskStatusSummaryToExcel(result.Value);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"TaskStatusSummary_{DateTime.Today:yyyyMMdd}.xlsx");
        }

        [HttpGet("team-productivity")]
        [ProducesResponseType(typeof(PagedResult<TeamProductivityReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<PagedResult<TeamProductivityReportDto>>> GetTeamProductivity(
            [FromQuery] string? search,
            [FromQuery] PaginationQuery? paging = null)
        {
            if (paging == null || (!paging.Page.HasValue && !paging.Limit.HasValue))
            {
                var full = await _reportService.GetTeamProductivityAsync(search, User.GetRoleId(), User.GetUserId());
                return StatusCode(full.StatusCode, full);
            }

            var page = PaginationExtensions.NormalizePage(paging?.Page);
            var limit = PaginationExtensions.NormalizePageSize(paging?.Limit ?? 0);
            var result = await _reportService.GetPagedTeamProductivityAsync(search, User.GetRoleId(), User.GetUserId(), page, limit);
            return Ok(result);
        }

        [HttpGet("team-productivity/excel")]
        public async Task<IActionResult> DownloadTeamProductivityExcel([FromQuery] string? search)
        {
            var result = await _reportService.GetTeamProductivityAsync(search, User.GetRoleId(), User.GetUserId());
            if (!result.IsSuccess || result.Value == null)
                return BadRequest(new { message = result.ErrorMessage });
            var bytes = _reportService.ExportTeamProductivityToExcel(result.Value);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"TeamProductivity_{DateTime.Today:yyyyMMdd}.xlsx");
        }

        [HttpGet("overdue-critical")]
        [ProducesResponseType(typeof(PagedResult<OverdueCriticalTaskDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<PagedResult<OverdueCriticalTaskDto>>> GetOverdueCritical(
            [FromQuery] string? search,
            [FromQuery] long? projectId,
            [FromQuery] int? priorityId,
            [FromQuery] string? delayType,
            [FromQuery] bool? assignedToMe,
            [FromQuery] bool? assignedToMyTeam,
            [FromQuery] PaginationQuery? paging = null)
        {
            if (paging == null || (!paging.Page.HasValue && !paging.Limit.HasValue))
            {
                var full = await _reportService.GetOverdueCriticalTasksAsync(
                    search, projectId, User.GetRoleId(), User.GetUserId(), priorityId, delayType, assignedToMe, assignedToMyTeam);
                return StatusCode(full.StatusCode, full);
            }

            var page = PaginationExtensions.NormalizePage(paging?.Page);
            var limit = PaginationExtensions.NormalizePageSize(paging?.Limit ?? 0);
            var result = await _reportService.GetPagedOverdueCriticalTasksAsync(
                search, projectId, User.GetRoleId(), User.GetUserId(), page, limit, priorityId, delayType, assignedToMe, assignedToMyTeam);
            return Ok(result);
        }

        [HttpGet("overdue-critical/excel")]
        public async Task<IActionResult> DownloadOverdueCriticalExcel(
            [FromQuery] string? search,
            [FromQuery] long? projectId,
            [FromQuery] bool? assignedToMe,
            [FromQuery] bool? assignedToMyTeam)
        {
            var result = await _reportService.GetOverdueCriticalTasksAsync(
                search, projectId, User.GetRoleId(), User.GetUserId(), assignedToMe: assignedToMe, assignedToMyTeam: assignedToMyTeam);
            if (!result.IsSuccess || result.Value == null)
                return BadRequest(new { message = result.ErrorMessage });
            var bytes = _reportService.ExportOverdueCriticalToExcel(result.Value);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"OverdueCriticalTasks_{DateTime.Today:yyyyMMdd}.xlsx");
        }

        [HttpGet("employee-productivity")]
        [ProducesResponseType(typeof(PagedResult<EmployeeProductivityReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<PagedResult<EmployeeProductivityReportDto>>> GetEmployeeProductivity(
            [FromQuery] string? search,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? status,
            [FromQuery] bool? assignedToMe,
            [FromQuery] bool? assignedToMyTeam,
            [FromQuery] PaginationQuery? paging = null)
        {
            (startDate, endDate) = NormalizeDateRange(startDate, endDate);
            if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
            {
                return BadRequest(Result.Failure("End date cannot be earlier than start date.", 400));
            }

            if (paging == null || (!paging.Page.HasValue && !paging.Limit.HasValue))
            {
                var reportData = await _reportService.GetEmployeeProductivityReportAsync(
                    search, startDate, endDate, status, User.GetRoleId(), User.GetUserId(), assignedToMe, assignedToMyTeam);
                return StatusCode(reportData.StatusCode, reportData);
            }

            var reportDataPaged = await _reportService.GetPagedEmployeeProductivityReportAsync(
                search, startDate, endDate, status, User.GetRoleId(), User.GetUserId(),
                paging.Page ?? 1, paging.Limit ?? 10, assignedToMe, assignedToMyTeam);
            return Ok(reportDataPaged);
        }

        [HttpGet("project-progress")]
        [ProducesResponseType(typeof(PagedResult<ProjectProgressReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<PagedResult<ProjectProgressReportDto>>> GetProjectProgress(
            [FromQuery] string? search,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? status,
            [FromQuery] bool? assignedToMe,
            [FromQuery] bool? assignedToMyTeam,
            [FromQuery] PaginationQuery? paging = null)
        {
            (startDate, endDate) = NormalizeDateRange(startDate, endDate);
            if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
            {
                return BadRequest(Result.Failure("End date cannot be earlier than start date.", 400));
            }

            if (paging == null || (!paging.Page.HasValue && !paging.Limit.HasValue))
            {
                var reportData = await _reportService.GetProjectProgressReportAsync(
                    search, startDate, endDate, status, User.GetRoleId(), User.GetUserId(), assignedToMe, assignedToMyTeam);
                return StatusCode(reportData.StatusCode, reportData);
            }

            var reportDataPaged = await _reportService.GetPagedProjectProgressReportAsync(
                search, startDate, endDate, status, User.GetRoleId(), User.GetUserId(),
                paging.Page ?? 1, paging.Limit ?? 10, assignedToMe, assignedToMyTeam);
            return Ok(reportDataPaged);
        }

        private static (DateTime? StartDate, DateTime? EndDate) NormalizeDateRange(DateTime? startDate, DateTime? endDate)
        {
            static DateTime? Normalize(DateTime? value) => value.HasValue
                ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc)
                : null;

            return (Normalize(startDate), Normalize(endDate));
        }
    }
}
