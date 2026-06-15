using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Dashboard;

namespace TaskTrackingSystem.WebApi.Features.Dashboard
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly DashboardService _dashboardService;

        public DashboardController(DashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<Result<DashboardSummaryDto>>> GetSummary()
        {
            var result = await _dashboardService.GetSummaryAsync(true);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("tasks-overview")]
        public async Task<ActionResult<Result<IEnumerable<TaskStatusOverviewDto>>>> GetTasksOverview()
        {
            var result = await _dashboardService.GetTasksOverviewAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("project-progress")]
        public async Task<ActionResult<Result<IEnumerable<ProjectProgressDto>>>> GetProjectProgress()
        {
            var result = await _dashboardService.GetProjectProgressAsync();
            return StatusCode(result.StatusCode, result);
        }
    }
}
