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

        public TaskService(AppDbContext db, TaskTrackingSystem.WebApi.Features.Notification.FirebaseNotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
        }

        private static DateTime? GetCompletedAt(TaskTrackingSystem.Database.AppDbContextModels.Task task)
        {
            return task.StatusId == AppTaskStatus.Done
                ? task.UpdatedAt ?? task.CreatedAt
                : null;
        }

        public async Task<IEnumerable<TaskDto>> GetAllTasksAsync(string roleName, long currentUserId)
        {
            return await BuildAccessibleTaskQuery(roleName, currentUserId)
                .OrderBy(t => t.DueDate)
                .ThenByDescending(t => t.CreatedAt ?? DateTime.UtcNow)
                .Select(t => new TaskDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
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
            string roleName,
            long currentUserId,
            string? search,
            long? projectId,
            AppTaskStatus? statusId,
            TaskPriority? priorityId,
            bool assignedOnly,
            int page,
            int pageSize)
        {
            var query = BuildAccessibleTaskQuery(roleName, currentUserId);

            if (assignedOnly)
            {
                query = query.Where(t => t.AssignedTo == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                query = query.Where(t =>
                    (t.Title != null && t.Title.ToLower().Contains(searchTerm)) ||
                    (t.Description != null && t.Description.ToLower().Contains(searchTerm)));
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
                .OrderBy(t => t.DueDate)
                .ThenByDescending(t => t.CreatedAt ?? DateTime.UtcNow)
                .Select(t => new TaskDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
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

        public async Task<IEnumerable<TaskDto>> GetArchivedTasksAsync(string roleName, long currentUserId)
        {
            return await BuildAccessibleTaskQuery(roleName, currentUserId, includeArchived: true)
                .Where(t => t.IsArchived)
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt ?? DateTime.UtcNow)
                .Select(t => new TaskDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
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

        public async Task<TaskDto?> GetTaskByIdAsync(long id, string roleName, long currentUserId)
        {
            var task = await BuildAccessibleTaskQuery(roleName, currentUserId)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return null;
            }

            return new TaskDto
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
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

        public async Task<Result<TaskDto>> CreateTaskAsync(CreateTaskDto dto, string roleName, long currentUserId)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
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

            if (!await CanAccessProjectAsync(dto.ProjectId, roleName, currentUserId))
            {
                return Result<TaskDto>.Failure("You do not have access to this project.", 403);
            }

            if (dto.AssignedTo.HasValue && !await IsProjectMemberAsync(dto.ProjectId, dto.AssignedTo.Value))
            {
                return Result<TaskDto>.Failure("Task assignee must belong to the selected project.", 400);
            }

            var task = new TaskTrackingSystem.Database.AppDbContextModels.Task
            {
                Title = dto.Title,
                Description = dto.Description,
                ProjectId = dto.ProjectId,
                StatusId = dto.StatusId == 0 ? AppTaskStatus.Todo : dto.StatusId,
                PriorityId = dto.PriorityId == 0 ? TaskPriority.Medium : dto.PriorityId,
                AssignedTo = dto.AssignedTo,
                AssignedBy = dto.AssignedBy,
                DueDate = dto.DueDate,
                IsDeleted = false,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserId
            };

            _db.Tasks.Add(task);
            await _db.SaveChangesAsync();

            if (task.AssignedTo.HasValue)
            {
                await _notificationService.NotifyTaskAssignedAsync(task, currentUserId);
            }

            var resultDto = new TaskDto
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
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
            if (string.IsNullOrWhiteSpace(dto.Title))
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

            task.Title = dto.Title;
            task.Description = dto.Description;
            task.StatusId = dto.StatusId;
            task.PriorityId = dto.PriorityId;
            task.AssignedTo = dto.AssignedTo;
            task.AssignedBy = dto.AssignedBy;
            task.DueDate = dto.DueDate;
            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedBy = currentUserId;

            _db.Tasks.Update(task);
            await _db.SaveChangesAsync();

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
            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        public async Task<Result<IEnumerable<TaskDto>>> GetTasksByUserIdAsync(long userId, string roleName, long currentUserId)
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted);
            if (!userExists)
            {
                return Result<IEnumerable<TaskDto>>.Failure(ResultMessages.UserNotFound(userId), 404);
            }

            var tasks = await BuildAccessibleTaskQuery(roleName, currentUserId)
                .Where(t => t.AssignedTo == userId)
                .Select(t => new TaskDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
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

        public async Task<Result> UpdateTaskStatusAsync(long id, AppTaskStatus statusId, string roleName, long currentUserId)
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

        public async Task<Result<int>> ArchiveDoneTasksAsync(long? projectId, string roleName, long currentUserId)
        {
            if (projectId.HasValue && projectId.Value > 0)
            {
                var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId.Value && p.IsDeleted != true);
                if (!projectExists)
                {
                    return Result<int>.Failure(ResultMessages.ProjectNotFound(projectId.Value), 404);
                }

                if (!await CanAccessProjectAsync(projectId.Value, roleName, currentUserId))
                {
                    return Result<int>.Failure("You do not have access to this project.", 403);
                }
            }

            var query = BuildAccessibleTaskQuery(roleName, currentUserId)
                .Where(t => t.StatusId == AppTaskStatus.Done);

            if (projectId.HasValue && projectId.Value > 0)
            {
                query = query.Where(t => t.ProjectId == projectId.Value);
            }

            var tasksToArchive = await query.ToListAsync();
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

        private IQueryable<TaskTrackingSystem.Database.AppDbContextModels.Task> BuildAccessibleTaskQuery(string roleName, long currentUserId, bool includeArchived = false)
        {
            var query = _db.Tasks.Where(t => t.IsDeleted != true && (includeArchived || !t.IsArchived));

            if (IsAdmin(roleName))
            {
                return query;
            }

            if (IsManager(roleName))
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

        private async Task<bool> CanAccessProjectAsync(long projectId, string roleName, long currentUserId)
        {
            if (IsAdmin(roleName))
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

        private static bool IsAdmin(string roleName)
        {
            return roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsManager(string roleName)
        {
            return roleName.Equals("Manager", StringComparison.OrdinalIgnoreCase);
        }
    }
}
