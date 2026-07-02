using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Task;
using TaskTrackingSystem.WebApi.Features.Task;
using TaskTrackingSystem.WebApi.Infrastructure;
using TaskTrackingSystem.Shared.Enums;

namespace TaskTrackingSystem.WebApi.Features.Task
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TaskController : ControllerBase
    {
        private readonly TaskService _taskService;
        private readonly PermissionAuthorizationService _permissionAuthorizationService;

        public TaskController(TaskService taskService, PermissionAuthorizationService permissionAuthorizationService)
        {
            _taskService = taskService;
            _permissionAuthorizationService = permissionAuthorizationService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<TaskDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> GetTasks(
            [FromQuery] string? search,
            [FromQuery] long? projectId,
            [FromQuery] AppTaskStatus? statusId,
            [FromQuery] TaskPriority? priorityId,
            [FromQuery] bool assignedOnly = false,
            [FromQuery] PaginationQuery? paging = null)
        {
            if (paging == null || (!paging.Page.HasValue && !paging.Limit.HasValue))
            {
                var tasks = await _taskService.GetAllTasksAsync(User.GetRoleId(), User.GetUserId());
                return Ok(tasks);
            }

            var page = PaginationExtensions.NormalizePage(paging.Page);
            var limit = PaginationExtensions.NormalizePageSize(paging.Limit ?? 0);
            var paged = await _taskService.GetPagedTasksAsync(
                User.GetRoleId(),
                User.GetUserId(),
                search,
                projectId,
                statusId,
                priorityId,
                assignedOnly,
                page,
                limit);

            return Ok(paged);
        }

        [HttpGet("archived")]
        public async Task<ActionResult<IEnumerable<TaskDto>>> GetArchivedTasks()
        {
            var tasks = await _taskService.GetArchivedTasksAsync(User.GetRoleId(), User.GetUserId());
            return Ok(tasks);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TaskDto>> GetTask(long id)
        {
            var task = await _taskService.GetTaskByIdAsync(id, User.GetRoleId(), User.GetUserId());
            if (task == null)
            {
                return NotFound(new { message = $"Task with ID {id} not found." });
            }
            return Ok(task);
        }

        [HttpPost]
        public async Task<ActionResult<Result<TaskDto>>> CreateTask([FromBody] CreateTaskDto createTaskDto)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Task", "Create"))
            {
                return Forbid();
            }

            var currentUserId = User.GetUserId();
            var result = await _taskService.CreateTaskAsync(createTaskDto, User.GetRoleId(), currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Result>> UpdateTask(long id, [FromBody] UpdateTaskDto updateTaskDto)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Task", "Update"))
            {
                return Forbid();
            }

            long? currentUserId = User.GetUserId();
            var result = await _taskService.UpdateTaskAsync(id, updateTaskDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<Result>> DeleteTask(long id)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Task", "Delete"))
            {
                return Forbid();
            }

            var result = await _taskService.SoftDeleteTaskAsync(id, User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("/api/User/{userId}/tasks")]
        public async Task<ActionResult<Result<IEnumerable<TaskDto>>>> GetUserTasks(long userId)
        {
            var result = await _taskService.GetTasksByUserIdAsync(userId, User.GetRoleId(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateTaskStatus(long id, [FromQuery] AppTaskStatus statusId)
        {
            var result = await _taskService.UpdateTaskStatusAsync(id, statusId, User.GetRoleId(), User.GetUserId());
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }
            return Ok(result);
        }

        [HttpPatch("archive-done")]
        public async Task<ActionResult<Result<int>>> ArchiveDoneTasks([FromQuery] long? projectId)
        {
            if (!await _permissionAuthorizationService.CanAccessAsync(User, "api/Task", "Update"))
            {
                return Forbid();
            }

            var result = await _taskService.ArchiveDoneTasksAsync(projectId, User.GetRoleId(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }
    }
}
