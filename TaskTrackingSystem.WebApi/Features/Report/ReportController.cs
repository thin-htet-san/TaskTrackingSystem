using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Report;

namespace TaskTrackingSystem.WebApi.Features.Report
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class ReportController : ControllerBase
    {
        private readonly ReportService _reportService;

        public ReportController(ReportService reportService)
        {
            _reportService = reportService;
        }

        // ─── Legacy ───────────────────────────────────────────────────────────────

        [HttpGet("tasks")]
        public async Task<ActionResult<Result<IEnumerable<TaskReportDto>>>> GetTasksReport(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? status,
            [FromQuery] int? projectId)
        {
            var result = await _reportService.GetTasksReportAsync(startDate, endDate, status, projectId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("user-productivity")]
        public async Task<ActionResult<Result<IEnumerable<UserProductivityDto>>>> GetUserProductivityReport()
        {
            var result = await _reportService.GetUserProductivityReportAsync();
            return StatusCode(result.StatusCode, result);
        }

        // ─── Report 1: Task Status Summary ────────────────────────────────────────

        [HttpGet("task-status-summary")]
        public async Task<ActionResult<Result<IEnumerable<TaskStatusSummaryDto>>>> GetTaskStatusSummary(
            [FromQuery] string? search,
            [FromQuery] long? statusId,
            [FromQuery] long? projectId)
        {
            var result = await _reportService.GetTaskStatusSummaryAsync(search, statusId, projectId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("task-status-summary/excel")]
        public async Task<IActionResult> DownloadTaskStatusSummaryExcel(
            [FromQuery] string? search,
            [FromQuery] long? statusId,
            [FromQuery] long? projectId)
        {
            var result = await _reportService.GetTaskStatusSummaryAsync(search, statusId, projectId);
            if (!result.IsSuccess || result.Value == null)
                return BadRequest(new { message = result.ErrorMessage });
            var bytes = _reportService.ExportTaskStatusSummaryToExcel(result.Value);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"TaskStatusSummary_{DateTime.Today:yyyyMMdd}.xlsx");
        }

        // ─── Report 2: Team Productivity ──────────────────────────────────────────

        [HttpGet("team-productivity")]
        public async Task<ActionResult<Result<IEnumerable<TeamProductivityReportDto>>>> GetTeamProductivity(
            [FromQuery] string? search)
        {
            var result = await _reportService.GetTeamProductivityAsync(search);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("team-productivity/excel")]
        public async Task<IActionResult> DownloadTeamProductivityExcel([FromQuery] string? search)
        {
            var result = await _reportService.GetTeamProductivityAsync(search);
            if (!result.IsSuccess || result.Value == null)
                return BadRequest(new { message = result.ErrorMessage });
            var bytes = _reportService.ExportTeamProductivityToExcel(result.Value);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"TeamProductivity_{DateTime.Today:yyyyMMdd}.xlsx");
        }

        // ─── Report 3: Overdue & Critical Tasks ───────────────────────────────────

        [HttpGet("overdue-critical")]
        public async Task<ActionResult<Result<IEnumerable<OverdueCriticalTaskDto>>>> GetOverdueCritical(
            [FromQuery] string? search,
            [FromQuery] long? projectId)
        {
            var result = await _reportService.GetOverdueCriticalTasksAsync(search, projectId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("overdue-critical/excel")]
        public async Task<IActionResult> DownloadOverdueCriticalExcel(
            [FromQuery] string? search,
            [FromQuery] long? projectId)
        {
            var result = await _reportService.GetOverdueCriticalTasksAsync(search, projectId);
            if (!result.IsSuccess || result.Value == null)
                return BadRequest(new { message = result.ErrorMessage });
            var bytes = _reportService.ExportOverdueCriticalToExcel(result.Value);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"OverdueCriticalTasks_{DateTime.Today:yyyyMMdd}.xlsx");
        }
    }
}
