using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Dashboard;
using TaskTrackingSystem.WebApi.Infrastructure;

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
            var result = await _dashboardService.GetSummaryAsync(User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("tasks-overview")]
        public async Task<ActionResult<Result<IEnumerable<TaskStatusOverviewDto>>>> GetTasksOverview()
        {
            var result = await _dashboardService.GetTasksOverviewAsync(User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("project-progress")]
        public async Task<ActionResult<Result<IEnumerable<ProjectProgressDto>>>> GetProjectProgress()
        {
            var result = await _dashboardService.GetProjectProgressAsync(User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("widgets")]
        public async Task<ActionResult<Result<IEnumerable<DashboardWidgetDto>>>> GetWidgets()
        {
            var result = await _dashboardService.GetWidgetsAsync(User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("widgets/layout")]
        public async Task<ActionResult<Result>> SaveWidgetLayout([FromBody] DashboardWidgetLayoutSaveRequestDto request)
        {
            var result = await _dashboardService.SaveWidgetLayoutAsync(User.GetRoleName(), User.GetUserId(), request);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("admin/widgets")]
        public async Task<ActionResult<Result<IEnumerable<DashboardWidgetAdminDto>>>> GetWidgetCatalog()
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var result = await _dashboardService.GetWidgetCatalogAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("admin/widgets/{widgetId:long}")]
        public async Task<ActionResult<Result<DashboardWidgetAdminDto>>> GetWidget(long widgetId)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var result = await _dashboardService.GetWidgetByIdAsync(widgetId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("admin/widgets")]
        public async Task<ActionResult<Result<DashboardWidgetAdminDto>>> CreateWidget([FromBody] DashboardWidgetUpsertDto request)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var result = await _dashboardService.SaveWidgetAsync(request, User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("admin/widgets/{widgetId:long}")]
        public async Task<ActionResult<Result<DashboardWidgetAdminDto>>> UpdateWidget(long widgetId, [FromBody] DashboardWidgetUpsertDto request)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            request.WidgetId = widgetId;
            var result = await _dashboardService.SaveWidgetAsync(request, User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("admin/widgets/{widgetId:long}")]
        public async Task<ActionResult<Result>> DeleteWidget(long widgetId)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var result = await _dashboardService.DeleteWidgetAsync(widgetId, User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("admin/widgets/{widgetId:long}/access")]
        public async Task<ActionResult<Result<IEnumerable<DashboardWidgetRoleAccessDto>>>> GetWidgetAccess(long widgetId)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var result = await _dashboardService.GetWidgetAccessAsync(widgetId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("admin/widgets/{widgetId:long}/access")]
        public async Task<ActionResult<Result>> SaveWidgetAccess(long widgetId, [FromBody] DashboardWidgetRoleAccessSaveRequestDto request)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var result = await _dashboardService.SaveWidgetAccessAsync(widgetId, request, User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }
    }
}
