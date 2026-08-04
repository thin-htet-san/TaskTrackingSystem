using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Enums;
using TaskTrackingSystem.Shared.Models.Task;
using TaskTrackingSystem.WebApi.Infrastructure;

namespace TaskTrackingSystem.WebApi.Features.Task
{
    public class TaskService
    {
        private readonly AppDbContext _db;
        private readonly TaskTrackingSystem.WebApi.Features.Notification.FirebaseNotificationService _notificationService;
        private readonly AuditLogService _auditLog;

        public TaskService(
            AppDbContext db,
            TaskTrackingSystem.WebApi.Features.Notification.FirebaseNotificationService notificationService,
            AuditLogService auditLog)
        {
            _db = db;
            _notificationService = notificationService;
            _auditLog = auditLog;
        }

        private static DateTime? GetCompletedAt(TaskTrackingSystem.Database.AppDbContextModels.Task task)
        {
            return task.StatusId == AppTaskStatus.Done
                ? task.UpdatedAt ?? task.CreatedAt
                : null;
        }

        private static DateTime ToUtcDate(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
        }

        public async Task<IEnumerable<TaskDto>> GetAllTasksAsync(long roleId, long currentUserId)
        {
            var query = await BuildAccessibleTaskQueryAsync(roleId, currentUserId);
            return await query
                .OrderByDescending(t => t.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(t => t.Id)
                .Select(t => new TaskDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    TitleMy = t.TitleMy,
                    Description = t.Description,
                    DescriptionMy = t.DescriptionMy,
                    ProjectId = t.ProjectId,
                    StatusId = t.StatusId,
                    PriorityId = t.PriorityId,
                    AssignedTo = t.AssignedTo,
                    AssignedBy = t.AssignedBy,
                    DueDate = t.DueDate,
                    CreatedAt = t.CreatedAt ?? DateTime.UtcNow,
                    IsArchived = t.IsArchived,
                    CompletedAt = GetCompletedAt(t)
                })
                .ToListAsync();
        }

        public async Task<PagedResult<TaskDto>> GetPagedTasksAsync(
            long roleId,
            long currentUserId,
            string? search,
            long? projectId,
            AppTaskStatus? statusId,
            TaskPriority? priorityId,
            bool assignedOnly,
            int page,
            int pageSize)
        {
            var query = await BuildAccessibleTaskQueryAsync(roleId, currentUserId);

            if (assignedOnly)
            {
                query = query.Where(t => t.AssignedTo == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                query = query.Where(t =>
                    (t.Title != null && t.Title.ToLower().Contains(searchTerm)) ||
                    (t.TitleMy != null && t.TitleMy.ToLower().Contains(searchTerm)) ||
                    (t.Description != null && t.Description.ToLower().Contains(searchTerm)) ||
                    (t.DescriptionMy != null && t.DescriptionMy.ToLower().Contains(searchTerm)));
            }

            if (projectId.HasValue && projectId.Value > 0)
            {
                query = query.Where(t => t.ProjectId == projectId.Value);
            }

            if (statusId.HasValue)
            {
                query = query.Where(t => t.StatusId == statusId.Value);
            }

            if (priorityId.HasValue)
            {
                query = query.Where(t => t.PriorityId == priorityId.Value);
            }

            return await query
                .OrderByDescending(t => t.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(t => t.Id)
                .Select(t => new TaskDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    TitleMy = t.TitleMy,
                    Description = t.Description,
                    DescriptionMy = t.DescriptionMy,
                    ProjectId = t.ProjectId,
                    StatusId = t.StatusId,
                    PriorityId = t.PriorityId,
                    AssignedTo = t.AssignedTo,
                    AssignedBy = t.AssignedBy,
                    DueDate = t.DueDate,
                    CreatedAt = t.CreatedAt ?? DateTime.UtcNow,
                    IsArchived = t.IsArchived,
                    CompletedAt = GetCompletedAt(t)
                })
                .ToPagedResultAsync(page, pageSize);
        }

        public async Task<IEnumerable<TaskDto>> GetArchivedTasksAsync(long roleId, long currentUserId)
        {
            var query = await BuildAccessibleTaskQueryAsync(roleId, currentUserId, includeArchived: true);
            return await query
                .Where(t => t.IsArchived)
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt ?? DateTime.UtcNow)
                .Select(t => new TaskDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    TitleMy = t.TitleMy,
                    Description = t.Description,
                    DescriptionMy = t.DescriptionMy,
                    ProjectId = t.ProjectId,
                    StatusId = t.StatusId,
                    PriorityId = t.PriorityId,
                    AssignedTo = t.AssignedTo,
                    AssignedBy = t.AssignedBy,
                    DueDate = t.DueDate,
                    CreatedAt = t.CreatedAt ?? DateTime.UtcNow,
                    IsArchived = t.IsArchived,
                    CompletedAt = GetCompletedAt(t)
                })
                .ToListAsync();
        }

        public async Task<TaskDto?> GetTaskByIdAsync(long id, long roleId, long currentUserId)
        {
            var query = await BuildAccessibleTaskQueryAsync(roleId, currentUserId);
            var task = await query.FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return null;
            }

            return new TaskDto
            {
                Id = task.Id,
                Title = task.Title,
                TitleMy = task.TitleMy,
                Description = task.Description,
                DescriptionMy = task.DescriptionMy,
                ProjectId = task.ProjectId,
                StatusId = task.StatusId,
                PriorityId = task.PriorityId,
                AssignedTo = task.AssignedTo,
                AssignedBy = task.AssignedBy,
                DueDate = task.DueDate,
                CreatedAt = task.CreatedAt ?? DateTime.UtcNow,
                IsArchived = task.IsArchived,
                CompletedAt = GetCompletedAt(task)
            };
        }

        public async Task<Result<TaskDto>> CreateTaskAsync(CreateTaskDto dto, long roleId, long currentUserId)
        {
            if (string.IsNullOrWhiteSpace(dto.Title) && string.IsNullOrWhiteSpace(dto.TitleMy))
            {
                return Result<TaskDto>.Failure(ResultMessages.TaskTitleRequired, 400);
            }

            if (dto.ProjectId == 0)
            {
                return Result<TaskDto>.Failure(ResultMessages.SelectProjectRequired, 400);
            }

            var projectExists = await _db.Projects.AnyAsync(p => p.Id == dto.ProjectId && p.IsDeleted != true);
            if (!projectExists)
            {
                return Result<TaskDto>.Failure(ResultMessages.ProjectNotFound(dto.ProjectId), 404);
            }

            if (!await CanAccessProjectAsync(dto.ProjectId, roleId, currentUserId))
            {
                return Result<TaskDto>.Failure("You do not have access to this project.", 403);
            }

            if (dto.AssignedTo.HasValue && !await IsProjectMemberAsync(dto.ProjectId, dto.AssignedTo.Value))
            {
                return Result<TaskDto>.Failure("Task assignee must belong to the selected project.", 400);
            }

            var task = new TaskTrackingSystem.Database.AppDbContextModels.Task
            {
                Title = dto.Title ?? string.Empty,
                TitleMy = dto.TitleMy,
                Description = dto.Description,
                DescriptionMy = dto.DescriptionMy,
                ProjectId = dto.ProjectId,
                StatusId = dto.StatusId == 0 ? AppTaskStatus.Todo : dto.StatusId,
                PriorityId = dto.PriorityId == 0 ? TaskPriority.Medium : dto.PriorityId,
                AssignedTo = dto.AssignedTo,
                AssignedBy = dto.AssignedBy,
                DueDate = ToUtcDate(dto.DueDate),
                IsDeleted = false,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserId
            };

            _db.Tasks.Add(task);
            await _db.SaveChangesAsync();
            await _auditLog.LogAsync("Create", "Task", $"Created task '{task.Title}'");

            if (task.AssignedTo.HasValue)
            {
                await _notificationService.NotifyTaskAssignedAsync(task, currentUserId);
            }

            var resultDto = new TaskDto
            {
                Id = task.Id,
                Title = task.Title,
                TitleMy = task.TitleMy,
                Description = task.Description,
                DescriptionMy = task.DescriptionMy,
                ProjectId = task.ProjectId,
                StatusId = task.StatusId,
                PriorityId = task.PriorityId,
                AssignedTo = task.AssignedTo,
                AssignedBy = task.AssignedBy,
                DueDate = task.DueDate,
                CreatedAt = task.CreatedAt ?? DateTime.UtcNow,
                CompletedAt = null,
                IsArchived = task.IsArchived
            };

            return Result<TaskDto>.Success(resultDto, 201);
        }

        public async Task<Result> UpdateTaskAsync(long id, UpdateTaskDto dto, long? currentUserId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Title) && string.IsNullOrWhiteSpace(dto.TitleMy))
            {
                return Result.Failure(ResultMessages.TaskTitleRequired, 400);
            }

            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted != true && !t.IsArchived);
            if (task == null)
            {
                return Result.Failure(ResultMessages.TaskNotFound(id), 404);
            }

            var previousStatusId = task.StatusId;
            var previousAssignedTo = task.AssignedTo;

            if (dto.AssignedTo.HasValue && !await IsProjectMemberAsync(task.ProjectId, dto.AssignedTo.Value))
            {
                return Result.Failure("Task assignee must belong to the selected project.", 400);
            }

            task.Title = dto.Title ?? string.Empty;
            task.TitleMy = dto.TitleMy;
            task.Description = dto.Description;
            task.DescriptionMy = dto.DescriptionMy;
            task.StatusId = dto.StatusId;
            task.PriorityId = dto.PriorityId;
            task.AssignedTo = dto.AssignedTo;
            task.AssignedBy = dto.AssignedBy;
            task.DueDate = ToUtcDate(dto.DueDate);
            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedBy = currentUserId;

            _db.Tasks.Update(task);
            await _db.SaveChangesAsync();
            await _auditLog.LogAsync("Update", "Task", $"Updated task '{task.Title}'");

            if (previousAssignedTo != task.AssignedTo && task.AssignedTo.HasValue && currentUserId.HasValue)
            {
                await _notificationService.NotifyTaskAssignedAsync(task, currentUserId.Value);
            }

            if (previousStatusId != dto.StatusId && currentUserId.HasValue)
            {
                await _notificationService.NotifyTaskStatusChangedAsync(task, currentUserId.Value, previousStatusId, dto.StatusId);
            }

            return Result.Success(200);
        }

        public async Task<Result> SoftDeleteTaskAsync(long id, long currentUserId)
        {
            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted != true && !t.IsArchived);
            if (task == null)
            {
                return Result.Failure(ResultMessages.TaskNotFound(id), 404);
            }

            task.IsDeleted = true;
            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedBy = currentUserId;
            _db.Tasks.Update(task);

            var issuesToUpdate = await _db.Issues
                .Where(i => i.TaskId == id && i.IsDeleted != true)
                .ToListAsync();
            foreach (var issue in issuesToUpdate)
            {
                issue.IsDeleted = true;
                issue.UpdatedAt = DateTime.UtcNow;
                issue.UpdatedBy = currentUserId;
            }

            if (issuesToUpdate.Any())
            {
                _db.Issues.UpdateRange(issuesToUpdate);
            }

            await _db.SaveChangesAsync();
            await _auditLog.LogAsync("Delete", "Task", $"Deleted task '{task.Title}'");
            return Result.Success(200);
        }

        public async Task<Result<IEnumerable<TaskDto>>> GetTasksByUserIdAsync(long userId, long roleId, long currentUserId)
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted);
            if (!userExists)
            {
                return Result<IEnumerable<TaskDto>>.Failure(ResultMessages.UserNotFound(userId), 404);
            }

            var query = await BuildAccessibleTaskQueryAsync(roleId, currentUserId);
            var tasks = await query
                .Where(t => t.AssignedTo == userId)
                .Select(t => new TaskDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    TitleMy = t.TitleMy,
                    Description = t.Description,
                    DescriptionMy = t.DescriptionMy,
                    ProjectId = t.ProjectId,
                    StatusId = t.StatusId,
                    PriorityId = t.PriorityId,
                    AssignedTo = t.AssignedTo,
                    AssignedBy = t.AssignedBy,
                    DueDate = t.DueDate,
                    CreatedAt = t.CreatedAt ?? DateTime.UtcNow,
                    IsArchived = t.IsArchived,
                    CompletedAt = GetCompletedAt(t)
                })
                .ToListAsync();

            return Result<IEnumerable<TaskDto>>.Success(tasks);
        }

        public async Task<Result> UpdateTaskStatusAsync(long id, AppTaskStatus statusId, long roleId, long currentUserId)
        {
            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted != true && !t.IsArchived);
            if (task == null)
            {
                return Result.Failure(ResultMessages.TaskNotFound(id), 404);
            }

            var previousStatusId = task.StatusId;

            task.StatusId = statusId;
            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedBy = currentUserId;

            _db.Tasks.Update(task);
            await _db.SaveChangesAsync();

            if (previousStatusId != statusId)
            {
                await _notificationService.NotifyTaskStatusChangedAsync(task, currentUserId, previousStatusId, statusId);
            }

            return Result.Success(200);
        }

        public async Task<Result<int>> ArchiveDoneTasksAsync(long? projectId, long roleId, long currentUserId)
        {
            if (projectId.HasValue && projectId.Value > 0)
            {
                var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId.Value && p.IsDeleted != true);
                if (!projectExists)
                {
                    return Result<int>.Failure(ResultMessages.ProjectNotFound(projectId.Value), 404);
                }

                if (!await CanAccessProjectAsync(projectId.Value, roleId, currentUserId))
                {
                    return Result<int>.Failure("You do not have access to this project.", 403);
                }
            }

            var query = await BuildAccessibleTaskQueryAsync(roleId, currentUserId);
            var tasksToArchive = await query
                .Where(t => t.StatusId == AppTaskStatus.Done)
                .ToListAsync();

            if (projectId.HasValue && projectId.Value > 0)
            {
                tasksToArchive = tasksToArchive.Where(t => t.ProjectId == projectId.Value).ToList();
            }

            foreach (var task in tasksToArchive)
            {
                task.IsArchived = true;
                task.UpdatedAt = DateTime.UtcNow;
                task.UpdatedBy = currentUserId;
            }

            if (tasksToArchive.Count > 0)
            {
                _db.Tasks.UpdateRange(tasksToArchive);
                await _db.SaveChangesAsync();
            }

            return Result<int>.Success(tasksToArchive.Count);
        }

        private async Task<IQueryable<TaskTrackingSystem.Database.AppDbContextModels.Task>> BuildAccessibleTaskQueryAsync(long roleId, long currentUserId, bool includeArchived = false)
        {
            var query = _db.Tasks.Where(t => t.IsDeleted != true && (includeArchived || !t.IsArchived));

            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            if (isAdmin)
            {
                return query;
            }

            var isManager = await DataScopeAuthorization.IsManagerScopeAsync(_db, roleId);
            if (isManager)
            {
                return query.Where(t =>
                    t.AssignedTo == currentUserId ||
                    t.CreatedBy == currentUserId ||
                    t.Project.ProjectMembers.Any(pm => pm.UserId == currentUserId));
            }

            return query.Where(t =>
                t.AssignedTo == currentUserId ||
                t.CreatedBy == currentUserId);
        }

        private async Task<bool> CanAccessProjectAsync(long projectId, long roleId, long currentUserId)
        {
            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            if (isAdmin)
            {
                return await _db.Projects.AnyAsync(p => p.Id == projectId && p.IsDeleted != true);
            }

            return await _db.Projects.AnyAsync(p =>
                p.Id == projectId &&
                p.IsDeleted != true &&
                (p.CreatedById == currentUserId || p.ProjectMembers.Any(pm => pm.UserId == currentUserId)));
        }

        private async Task<bool> IsProjectMemberAsync(long projectId, long userId)
        {
            return await _db.ProjectMembers.AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
        }
    }
}
