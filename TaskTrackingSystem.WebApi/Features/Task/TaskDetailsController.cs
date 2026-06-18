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
using TaskTrackingSystem.Shared.Models.Attachment;
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

            await _notificationService.NotifyCommentAddedAsync(task, userId, $"{user.FirstName} {user.LastName}");

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

        // ─── ATTACHMENTS ──────────────────────────────────────────────────────

        [HttpGet("tasks/{taskId}/attachments")]
        public async Task<ActionResult<IEnumerable<FileAttachmentDto>>> GetAttachments(long taskId)
        {
            var taskExists = await _db.Tasks.AnyAsync(t => t.Id == taskId && !t.IsDeleted);
            if (!taskExists)
            {
                return NotFound(new { message = $"Task with ID {taskId} not found." });
            }

            var attachments = await _db.FileAttachments
                .Where(f => f.TaskId == taskId && !f.IsDeleted)
                .OrderBy(f => f.CreatedAt)
                .Select(f => new FileAttachmentDto
                {
                    Id = f.Id,
                    TaskId = f.TaskId,
                    FileName = f.FileName,
                    FileType = f.FileType,
                    FileSizeInBytes = f.FileSizeInBytes,
                    CreatedAt = f.CreatedAt ?? DateTime.UtcNow,
                    CreatedBy = f.CreatedBy,
                    CreatedByName = _db.Users
                        .Where(u => u.Id == f.CreatedBy)
                        .Select(u => $"{u.FirstName} {u.LastName}")
                        .FirstOrDefault() ?? "Unknown"
                })
                .ToListAsync();

            return Ok(attachments);
        }

        [HttpPost("tasks/{taskId}/attachments")]
        public async Task<ActionResult<Result<FileAttachmentDto>>> UploadAttachment(long taskId, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(Result<FileAttachmentDto>.Failure("No file was uploaded.", 400));
            }

            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && !t.IsDeleted);
            if (task == null)
            {
                return NotFound(Result<FileAttachmentDto>.Failure($"Task with ID {taskId} not found.", 404));
            }

            var userId = User.GetUserId();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return Unauthorized();
            }

            // Create uploads directory
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Generate unique name
            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // Save file physically
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var attachment = new FileAttachment
            {
                TaskId = taskId,
                FileName = file.FileName,
                FileType = file.ContentType,
                FilePath = uniqueFileName, // Store the unique disk filename as FilePath
                FileSizeInBytes = file.Length,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                IsDeleted = false
            };

            _db.FileAttachments.Add(attachment);
            await _db.SaveChangesAsync();

            var resultDto = new FileAttachmentDto
            {
                Id = attachment.Id,
                TaskId = attachment.TaskId,
                FileName = attachment.FileName,
                FileType = attachment.FileType,
                FileSizeInBytes = attachment.FileSizeInBytes,
                CreatedAt = attachment.CreatedAt ?? DateTime.UtcNow,
                CreatedBy = attachment.CreatedBy,
                CreatedByName = $"{user.FirstName} {user.LastName}"
            };

            return StatusCode(201, Result<FileAttachmentDto>.Success(resultDto, 201));
        }

        [HttpGet("attachments/{attachmentId}/download")]
        [AllowAnonymous] // Allow download endpoint to stream, we can check custom token validation or basic checks if desired, or let Blazor fetch it
        public async Task<IActionResult> DownloadAttachment(long attachmentId)
        {
            var attachment = await _db.FileAttachments.FirstOrDefaultAsync(f => f.Id == attachmentId && !f.IsDeleted);
            if (attachment == null)
            {
                return NotFound();
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            var filePath = Path.Combine(uploadsFolder, attachment.FilePath);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(bytes, attachment.FileType, attachment.FileName);
        }

        [HttpDelete("attachments/{attachmentId}")]
        public async Task<ActionResult<Result>> DeleteAttachment(long attachmentId)
        {
            var attachment = await _db.FileAttachments.FirstOrDefaultAsync(f => f.Id == attachmentId && !f.IsDeleted);
            if (attachment == null)
            {
                return NotFound(Result.Failure("Attachment not found.", 404));
            }

            var userId = User.GetUserId();
            var isAdmin = User.IsAdmin();

            // Only attachment creator or Admin can delete
            if (attachment.CreatedBy != userId && !isAdmin)
            {
                return Forbid();
            }

            attachment.IsDeleted = true;
            attachment.UpdatedAt = DateTime.UtcNow;
            attachment.UpdatedBy = userId;

            _db.FileAttachments.Update(attachment);
            await _db.SaveChangesAsync();

            // Optional: delete physical file or leave it for archiving. We'll leave it as soft-deleted in DB.
            return Ok(Result.Success(200));
        }
    }
}
