using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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

        public ProjectController(ProjectService projectService)
        {
            _projectService = projectService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectDto>>> GetProjects()
        {
            var projects = await _projectService.GetAllProjectsAsync(User.GetRoleName(), User.GetUserId());
            return Ok(projects);
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
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Result<ProjectDto>>> CreateProject([FromBody] CreateProjectDto createProjectDto)
        {
            long? currentUserId = User.GetUserId();
            var result = await _projectService.CreateProjectAsync(createProjectDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Result>> UpdateProject(long id, [FromBody] UpdateProjectDto updateProjectDto)
        {
            long? currentUserId = User.GetUserId();
            var result = await _projectService.UpdateProjectAsync(id, updateProjectDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Result>> DeleteProject(long id)
        {
            var result = await _projectService.SoftDeleteProjectAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}/members")]
        public async Task<ActionResult<Result<IEnumerable<UserDto>>>> GetProjectMembers(long id)
        {
            var result = await _projectService.GetProjectMembersAsync(id, User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/members")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignProjectMembers(long id, [FromBody] AssignMembersDto dto)
        {
            var result = await _projectService.AssignMembersToProjectAsync(id, dto, User.GetUserId());
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }
            return Ok();
        }

        [HttpDelete("{id}/members/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveProjectMember(long id, long userId)
        {
            var result = await _projectService.RemoveMemberFromProjectAsync(id, userId, User.GetUserId());
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
