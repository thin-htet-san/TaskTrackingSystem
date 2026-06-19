using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Comment;
using TaskTrackingSystem.WebApi.Infrastructure;

namespace TaskTrackingSystem.WebApi.Features.Task
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TaskDetailsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TaskTrackingSystem.WebApi.Features.Notification.FirebaseNotificationService _notificationService;

        public TaskDetailsController(AppDbContext db, TaskTrackingSystem.WebApi.Features.Notification.FirebaseNotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
        }

        // ─── COMMENTS ────────────────────────────────────────────────────────

        [HttpGet("tasks/{taskId}/comments")]
        public async Task<ActionResult<IEnumerable<CommentDto>>> GetComments(long taskId)
        {
            var taskExists = await _db.Tasks.AnyAsync(t => t.Id == taskId && !t.IsDeleted);
            if (!taskExists)
            {
                return NotFound(new { message = $"Task with ID {taskId} not found." });
            }

            var comments = await _db.Comments
                .Include(c => c.User)
                .ThenInclude(u => u.Role)
                .Where(c => c.TaskId == taskId && !c.IsDeleted)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new CommentDto
                {
                    Id = c.Id,
                    TaskId = c.TaskId,
                    UserId = c.UserId,
                    UserFullName = $"{c.User.FirstName} {c.User.LastName}",
                    UserRoleName = c.User.Role != null ? c.User.Role.Name : "User",
                    Message = c.Message,
                    CreatedAt = c.CreatedAt ?? DateTime.UtcNow,
                    UpdatedAt = c.UpdatedAt
                })
                .ToListAsync();

            return Ok(comments);
        }

        [HttpPost("tasks/{taskId}/comments")]
        public async Task<ActionResult<Result<CommentDto>>> CreateComment(long taskId, [FromBody] CreateCommentDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
            {
                return BadRequest(Result<CommentDto>.Failure("Comment message cannot be empty.", 400));
            }

            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && !t.IsDeleted);
            if (task == null)
            {
                return NotFound(Result<CommentDto>.Failure($"Task with ID {taskId} not found.", 404));
            }

            var userId = User.GetUserId();
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return Unauthorized();
            }

            var comment = new Comment
            {
                TaskId = taskId,
                UserId = userId,
                Message = dto.Message.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                IsDeleted = false
            };

            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();

            var actorName = $"{user.FirstName} {user.LastName}";
            await _notificationService.NotifyCommentAddedAsync(task, userId, actorName, comment.Message);
            await _notificationService.NotifyMentionedAsync(task, userId, actorName, comment.Message);

            var resultDto = new CommentDto
            {
                Id = comment.Id,
                TaskId = comment.TaskId,
                UserId = comment.UserId,
                UserFullName = $"{user.FirstName} {user.LastName}",
                UserRoleName = user.Role != null ? user.Role.Name : "User",
                Message = comment.Message,
                CreatedAt = comment.CreatedAt ?? DateTime.UtcNow
            };

            return StatusCode(201, Result<CommentDto>.Success(resultDto, 201));
        }

        [HttpDelete("comments/{commentId}")]
        public async Task<ActionResult<Result>> DeleteComment(long commentId)
        {
            var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted);
            if (comment == null)
            {
                return NotFound(Result.Failure("Comment not found.", 404));
            }

            var userId = User.GetUserId();
            var isAdmin = User.IsAdmin();

            // Only comment creator or Admin can delete
            if (comment.UserId != userId && !isAdmin)
            {
                return Forbid();
            }

            comment.IsDeleted = true;
            comment.UpdatedAt = DateTime.UtcNow;
            comment.UpdatedBy = userId;

            _db.Comments.Update(comment);
            await _db.SaveChangesAsync();

            return Ok(Result.Success(200));
        }
    }
}
