using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Enums;
using TaskTrackingSystem.Shared.Models.Issue;
using TaskTrackingSystem.WebApi.Infrastructure;

namespace TaskTrackingSystem.WebApi.Features.Issue
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class IssueController : ControllerBase
    {
        private readonly IssueService _issueService;
        private readonly PermissionAuthorizationService _permissionAuthorizationService;

        public IssueController(IssueService issueService, PermissionAuthorizationService permissionAuthorizationService)
        {
            _issueService = issueService;
            _permissionAuthorizationService = permissionAuthorizationService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<IssueDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult> GetIssues(
            [FromQuery] string? search,
            [FromQuery] long? taskId,
            [FromQuery] long? projectId,
            [FromQuery] AppTaskStatus? statusId,
            [FromQuery] TaskPriority? priorityId,
            [FromQuery] bool assignedOnly = false,
            [FromQuery] PaginationQuery? paging = null)
        {
            if (paging == null || (!paging.Page.HasValue && !paging.Limit.HasValue))
            {
                var issues = await _issueService.GetAllIssuesAsync(User.GetRoleName(), User.GetUserId());
                return Ok(issues);
            }

            var page = PaginationExtensions.NormalizePage(paging.Page);
            var limit = PaginationExtensions.NormalizePageSize(paging.Limit ?? 0);
            var paged = await _issueService.GetPagedIssuesAsync(
                User.GetRoleName(),
                User.GetUserId(),
                search,
                taskId,
                projectId,
                statusId,
                priorityId,
                assignedOnly,
                page,
                limit);

            return Ok(paged);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<IssueDto>> GetIssue(long id)
        {
            var issue = await _issueService.GetIssueByIdAsync(id, User.GetRoleName(), User.GetUserId());
            if (issue == null)
            {
                return NotFound(new { message = $"Issue with ID {id} not found." });
            }

            return Ok(issue);
        }

        [HttpGet("task/{taskId}")]
        public async Task<ActionResult<IEnumerable<IssueDto>>> GetIssuesByTask(long taskId)
        {
            var issues = await _issueService.GetIssuesByTaskIdAsync(taskId, User.GetRoleName(), User.GetUserId());
            return Ok(issues);
        }

        [HttpPost]
        public async Task<ActionResult<Result<IssueDto>>> CreateIssue([FromBody] CreateIssueDto dto)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Issue", "Create"))
            {
                return Forbid();
            }

            var result = await _issueService.CreateIssueAsync(dto, User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Result>> UpdateIssue(long id, [FromBody] UpdateIssueDto dto)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Issue", "Update"))
            {
                return Forbid();
            }

            var result = await _issueService.UpdateIssueAsync(id, dto, User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<Result>> DeleteIssue(long id)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Issue", "Delete"))
            {
                return Forbid();
            }

            var result = await _issueService.SoftDeleteIssueAsync(id, User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }
    }
}
