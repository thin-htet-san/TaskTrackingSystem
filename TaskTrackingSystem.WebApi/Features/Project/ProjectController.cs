using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.User;
using TaskTrackingSystem.Shared.Models.Task;
using TaskTrackingSystem.Shared.Models.Project;
using TaskTrackingSystem.WebApi.Infrastructure;

namespace TaskTrackingSystem.WebApi.Features.Project
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProjectController : ControllerBase
    {
        private readonly ProjectService _projectService;
        private readonly PermissionAuthorizationService _permissionAuthorizationService;

        public ProjectController(ProjectService projectService, PermissionAuthorizationService permissionAuthorizationService)
        {
            _projectService = projectService;
            _permissionAuthorizationService = permissionAuthorizationService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<ProjectDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> GetProjects(
            [FromQuery] string? search,
            [FromQuery] PaginationQuery? paging = null)
        {
            if (paging == null || (!paging.Page.HasValue && !paging.Limit.HasValue))
            {
                var projects = await _projectService.GetAllProjectsAsync(User.GetRoleName(), User.GetUserId());
                return Ok(projects);
            }

            var page = PaginationExtensions.NormalizePage(paging.Page);
            var limit = PaginationExtensions.NormalizePageSize(paging.Limit ?? 0);
            var paged = await _projectService.GetPagedProjectsAsync(User.GetRoleName(), User.GetUserId(), search, page, limit);
            return Ok(paged);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectDto>> GetProject(long id)
        {
            var project = await _projectService.GetProjectByIdAsync(id, User.GetRoleName(), User.GetUserId());
            if (project == null)
            {
                return NotFound(new { message = $"Project with ID {id} not found." });
            }
            return Ok(project);
        }

        [HttpPost]
        public async Task<ActionResult<Result<ProjectDto>>> CreateProject([FromBody] CreateProjectDto createProjectDto)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Project", "Create"))
            {
                return Forbid();
            }

            long? currentUserId = User.GetUserId();
            var result = await _projectService.CreateProjectAsync(createProjectDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Result>> UpdateProject(long id, [FromBody] UpdateProjectDto updateProjectDto)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Project", "Update"))
            {
                return Forbid();
            }

            var currentUserId = User.GetUserId();
            var result = await _projectService.UpdateProjectAsync(id, updateProjectDto, User.GetRoleName(), currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<Result>> DeleteProject(long id)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Project", "Delete"))
            {
                return Forbid();
            }

            var result = await _projectService.SoftDeleteProjectAsync(id, User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}/members")]
        public async Task<ActionResult<Result<IEnumerable<UserDto>>>> GetProjectMembers(long id)
        {
            var result = await _projectService.GetProjectMembersAsync(id, User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/members")]
        public async Task<IActionResult> AssignProjectMembers(long id, [FromBody] AssignMembersDto dto)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Project", "Update"))
            {
                return Forbid();
            }

            var result = await _projectService.AssignMembersToProjectAsync(id, dto, User.GetRoleName(), User.GetUserId());
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }
            return Ok();
        }

        [HttpDelete("{id}/members/{userId}")]
        public async Task<IActionResult> RemoveProjectMember(long id, long userId)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Project", "Update"))
            {
                return Forbid();
            }

            var result = await _projectService.RemoveMemberFromProjectAsync(id, userId, User.GetRoleName(), User.GetUserId());
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }
            return NoContent();
        }

        [HttpGet("{id}/tasks")]
        public async Task<ActionResult<Result<IEnumerable<TaskDto>>>> GetProjectTasks(long id)
        {
            var result = await _projectService.GetProjectTasksAsync(id, User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }
    }
}
