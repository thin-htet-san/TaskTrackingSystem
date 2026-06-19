using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Report;
using TaskTrackingSystem.Shared.Enums;
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

        // â”€â”€â”€ Legacy â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [HttpGet("tasks")]
        public async Task<ActionResult<Result<IEnumerable<TaskReportDto>>>> GetTasksReport(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? status,
            [FromQuery] int? projectId)
        {
            var result = await _reportService.GetTasksReportAsync(startDate, endDate, status, projectId, User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("user-productivity")]
        public async Task<ActionResult<Result<IEnumerable<UserProductivityDto>>>> GetUserProductivityReport()
        {
            var result = await _reportService.GetUserProductivityReportAsync(User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        // â”€â”€â”€ Report 1: Task Status Summary â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [HttpGet("task-status-summary")]
        public async Task<ActionResult<Result<IEnumerable<TaskStatusSummaryDto>>>> GetTaskStatusSummary(
            [FromQuery] string? search,
            [FromQuery] AppTaskStatus? statusId,
            [FromQuery] long? projectId)
        {
            var result = await _reportService.GetTaskStatusSummaryAsync(search, statusId, projectId, User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("task-status-summary/excel")]
        public async Task<IActionResult> DownloadTaskStatusSummaryExcel(
            [FromQuery] string? search,
            [FromQuery] AppTaskStatus? statusId,
            [FromQuery] long? projectId)
        {
            var result = await _reportService.GetTaskStatusSummaryAsync(search, statusId, projectId, User.GetRoleName(), User.GetUserId());
            if (!result.IsSuccess || result.Value == null)
                return BadRequest(new { message = result.ErrorMessage });
            var bytes = _reportService.ExportTaskStatusSummaryToExcel(result.Value);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"TaskStatusSummary_{DateTime.Today:yyyyMMdd}.xlsx");
        }

        // â”€â”€â”€ Report 2: Team Productivity â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [HttpGet("team-productivity")]
        public async Task<ActionResult<Result<IEnumerable<TeamProductivityReportDto>>>> GetTeamProductivity(
            [FromQuery] string? search)
        {
            var result = await _reportService.GetTeamProductivityAsync(search, User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("team-productivity/excel")]
        public async Task<IActionResult> DownloadTeamProductivityExcel([FromQuery] string? search)
        {
            var result = await _reportService.GetTeamProductivityAsync(search, User.GetRoleName(), User.GetUserId());
            if (!result.IsSuccess || result.Value == null)
                return BadRequest(new { message = result.ErrorMessage });
            var bytes = _reportService.ExportTeamProductivityToExcel(result.Value);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"TeamProductivity_{DateTime.Today:yyyyMMdd}.xlsx");
        }

        // â”€â”€â”€ Report 3: Overdue & Critical Tasks â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [HttpGet("overdue-critical")]
        public async Task<ActionResult<Result<IEnumerable<OverdueCriticalTaskDto>>>> GetOverdueCritical(
            [FromQuery] string? search,
            [FromQuery] long? projectId)
        {
            var result = await _reportService.GetOverdueCriticalTasksAsync(search, projectId, User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("overdue-critical/excel")]
        public async Task<IActionResult> DownloadOverdueCriticalExcel(
            [FromQuery] string? search,
            [FromQuery] long? projectId)
        {
            var result = await _reportService.GetOverdueCriticalTasksAsync(search, projectId, User.GetRoleName(), User.GetUserId());
            if (!result.IsSuccess || result.Value == null)
                return BadRequest(new { message = result.ErrorMessage });
            var bytes = _reportService.ExportOverdueCriticalToExcel(result.Value);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"OverdueCriticalTasks_{DateTime.Today:yyyyMMdd}.xlsx");
        }

        // â”€â”€â”€ Report 4: Time Tracking â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [HttpGet("time-tracking")]
        public async Task<ActionResult<Result<TimeTrackingReportDto>>> GetTimeTracking(
            [FromQuery] string? search,
            [FromQuery] long? projectId,
            [FromQuery] AppTaskStatus? statusId)
        {
            var result = await _reportService.GetTimeTrackingReportAsync(search, projectId, statusId, User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }
    }
}
