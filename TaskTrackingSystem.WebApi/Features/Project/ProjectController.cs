using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.User;
using TaskTrackingSystem.Shared.Models.Task;
using TaskTrackingSystem.Shared.Models.Project;

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
            var projects = await _projectService.GetAllProjectsAsync();
            return Ok(projects);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectDto>> GetProject(long id)
        {
            var project = await _projectService.GetProjectByIdAsync(id);
            if (project == null)
            {
                return NotFound(new { message = $"Project with ID {id} not found." });
            }
            return Ok(project);
        }

        [HttpPost]
        public async Task<ActionResult<Result<ProjectDto>>> CreateProject([FromBody] CreateProjectDto createProjectDto)
        {
            long? currentUserId = null;
            var result = await _projectService.CreateProjectAsync(createProjectDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Result>> UpdateProject(long id, [FromBody] UpdateProjectDto updateProjectDto)
        {
            long? currentUserId = null;
            var result = await _projectService.UpdateProjectAsync(id, updateProjectDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<Result>> DeleteProject(long id)
        {
            var result = await _projectService.SoftDeleteProjectAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}/members")]
        public async Task<ActionResult<Result<IEnumerable<UserDto>>>> GetProjectMembers(long id)
        {
            var result = await _projectService.GetProjectMembersAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/members")]
        public async Task<IActionResult> AssignProjectMembers(long id, [FromBody] AssignMembersDto dto)
        {
            var result = await _projectService.AssignMembersToProjectAsync(id, dto);
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }
            return Ok();
        }

        [HttpDelete("{id}/members/{userId}")]
        public async Task<IActionResult> RemoveProjectMember(long id, long userId)
        {
            var result = await _projectService.RemoveMemberFromProjectAsync(id, userId);
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }
            return NoContent();
        }

        [HttpGet("{id}/tasks")]
        public async Task<ActionResult<Result<IEnumerable<TaskDto>>>> GetProjectTasks(long id)
        {
            var result = await _projectService.GetProjectTasksAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
