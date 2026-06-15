using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Task;
using TaskTrackingSystem.WebApi.Features.Task;

namespace TaskTrackingSystem.WebApi.Features.Task
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TaskController : ControllerBase
    {
        private readonly TaskService _taskService;

        public TaskController(TaskService taskService)
        {
            _taskService = taskService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskDto>>> GetTasks()
        {
            var tasks = await _taskService.GetAllTasksAsync();
            return Ok(tasks);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TaskDto>> GetTask(long id)
        {
            var task = await _taskService.GetTaskByIdAsync(id);
            if (task == null)
            {
                return NotFound(new { message = $"Task with ID {id} not found." });
            }
            return Ok(task);
        }

        [HttpPost]
        public async Task<ActionResult<Result<TaskDto>>> CreateTask([FromBody] CreateTaskDto createTaskDto)
        {
            long? currentUserId = null;
            var result = await _taskService.CreateTaskAsync(createTaskDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Result>> UpdateTask(long id, [FromBody] UpdateTaskDto updateTaskDto)
        {
            long? currentUserId = null;
            var result = await _taskService.UpdateTaskAsync(id, updateTaskDto, currentUserId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<Result>> DeleteTask(long id)
        {
            var result = await _taskService.SoftDeleteTaskAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("/api/User/{userId}/tasks")]
        public async Task<ActionResult<Result<IEnumerable<TaskDto>>>> GetUserTasks(long userId)
        {
            var result = await _taskService.GetTasksByUserIdAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateTaskStatus(long id, [FromQuery] long statusId)
        {
            var result = await _taskService.UpdateTaskStatusAsync(id, statusId);
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }
            return Ok(result);
        }
    }
}
