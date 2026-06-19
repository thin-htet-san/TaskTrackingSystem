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
        public async Task<ActionResult<IEnumerable<TaskDto>>> GetTasks()
        {
            var tasks = await _taskService.GetAllTasksAsync(User.GetRoleName(), User.GetUserId());
            return Ok(tasks);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TaskDto>> GetTask(long id)
        {
            var task = await _taskService.GetTaskByIdAsync(id, User.GetRoleName(), User.GetUserId());
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
            var result = await _taskService.CreateTaskAsync(createTaskDto, User.GetRoleName(), currentUserId);
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
            var result = await _taskService.GetTasksByUserIdAsync(userId, User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateTaskStatus(long id, [FromQuery] AppTaskStatus statusId)
        {
            var result = await _taskService.UpdateTaskStatusAsync(id, statusId, User.GetRoleName(), User.GetUserId());
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

            var result = await _taskService.ArchiveDoneTasksAsync(projectId, User.GetRoleName(), User.GetUserId());
            return StatusCode(result.StatusCode, result);
        }
    }
}
