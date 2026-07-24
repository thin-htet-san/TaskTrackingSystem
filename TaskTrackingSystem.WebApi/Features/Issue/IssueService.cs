using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Enums;
using TaskTrackingSystem.Shared.Models.Issue;
using TaskTrackingSystem.WebApi.Infrastructure;
using IssueEntity = TaskTrackingSystem.Database.AppDbContextModels.Issue;

namespace TaskTrackingSystem.WebApi.Features.Issue
{
    public class IssueService
    {
        private readonly AppDbContext _db;

        public IssueService(AppDbContext db)
        {
            _db = db;
        }

        private static DateTime ToUtcDate(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
        }

        public async Task<IEnumerable<IssueDto>> GetAllIssuesAsync(long roleId, long currentUserId)
        {
            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            var isManager = await DataScopeAuthorization.IsManagerScopeAsync(_db, roleId);
            return await BuildAccessibleIssueQuery(isAdmin, isManager, currentUserId)
                .OrderByDescending(i => i.CreatedAt ?? DateTime.UtcNow)
                .Select(ToDtoProjection())
                .ToListAsync();
        }

        public async Task<PagedResult<IssueDto>> GetPagedIssuesAsync(
            long roleId,
            long currentUserId,
            string? search,
            long? taskId,
            long? projectId,
            AppTaskStatus? statusId,
            TaskPriority? priorityId,
            bool assignedOnly,
            int page,
            int pageSize)
        {
            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            var isManager = await DataScopeAuthorization.IsManagerScopeAsync(_db, roleId);
            var query = BuildAccessibleIssueQuery(isAdmin, isManager, currentUserId);

            if (assignedOnly)
            {
                query = query.Where(i => i.AssignedTo == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                query = query.Where(i =>
                    (i.Title != null && i.Title.ToLower().Contains(searchTerm)) ||
                    (i.Description != null && i.Description.ToLower().Contains(searchTerm)));
            }

            if (taskId.HasValue && taskId.Value > 0)
            {
                query = query.Where(i => i.TaskId == taskId.Value);
            }

            if (projectId.HasValue && projectId.Value > 0)
            {
                query = query.Where(i => i.Task.ProjectId == projectId.Value);
            }

            if (statusId.HasValue)
            {
                query = query.Where(i => i.StatusId == statusId.Value);
            }

            if (priorityId.HasValue)
            {
                query = query.Where(i => i.PriorityId == priorityId.Value);
            }

            return await query
                .OrderByDescending(i => i.CreatedAt ?? DateTime.UtcNow)
                .Select(ToDtoProjection())
                .ToPagedResultAsync(page, pageSize);
        }



        public async Task<IssueDto?> GetIssueByIdAsync(long id, long roleId, long currentUserId)
        {
            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            var isManager = await DataScopeAuthorization.IsManagerScopeAsync(_db, roleId);
            var issue = await BuildAccessibleIssueQuery(isAdmin, isManager, currentUserId)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (issue == null)
            {
                return null;
            }

            return await BuildAccessibleIssueQuery(isAdmin, isManager, currentUserId)
                .Where(i => i.Id == id)
                .Select(ToDtoProjection())
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<IssueDto>> GetIssuesByTaskIdAsync(long taskId, long roleId, long currentUserId)
        {
            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            if (!await CanAccessTaskAsync(taskId, isAdmin, currentUserId))
            {
                return Array.Empty<IssueDto>();
            }

            var isManager = await DataScopeAuthorization.IsManagerScopeAsync(_db, roleId);
            return await BuildAccessibleIssueQuery(isAdmin, isManager, currentUserId)
                .Where(i => i.TaskId == taskId)
                .OrderByDescending(i => i.CreatedAt ?? DateTime.UtcNow)
                .Select(ToDtoProjection())
                .ToListAsync();
        }

        public async Task<Result<IssueDto>> CreateIssueAsync(CreateIssueDto dto, long roleId, long currentUserId)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return Result<IssueDto>.Failure("Issue title is required.", 400);
            }

            var task = await _db.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == dto.TaskId && t.IsDeleted != true && !t.IsArchived && t.Project.IsDeleted != true);

            if (task == null)
            {
                return Result<IssueDto>.Failure($"Task with ID {dto.TaskId} not found.", 404);
            }

            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            if (!await CanAccessTaskAsync(task.Id, isAdmin, currentUserId))
            {
                return Result<IssueDto>.Failure("You do not have access to this task.", 403);
            }

            if (dto.AssignedTo.HasValue && !await IsTaskMemberAsync(task.ProjectId, dto.AssignedTo.Value))
            {
                return Result<IssueDto>.Failure("Issue assignee must belong to the selected project.", 400);
            }

            var issue = new IssueEntity
            {
                TaskId = dto.TaskId,
                Title = dto.Title.Trim(),
                Description = dto.Description,
                AssignedTo = dto.AssignedTo,
                EstimatedHours = dto.EstimatedHours,
                ActualHours = dto.ActualHours,
                DelayReason = dto.DelayReason,
                IsBlocked = dto.IsBlocked,
                BlockedBy = dto.BlockedBy,
                EscalationLevel = dto.EscalationLevel,
                StartDate = ToUtcDate(dto.StartDate),
                DueDate = ToUtcDate(dto.DueDate),
                StatusId = dto.StatusId == 0 ? AppTaskStatus.Todo : dto.StatusId,
                PriorityId = dto.PriorityId == 0 ? TaskPriority.Medium : dto.PriorityId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserId,
                IsDeleted = false
            };

            _db.Issues.Add(issue);
            await _db.SaveChangesAsync();

            var result = await GetIssueByIdAsync(issue.Id, roleId, currentUserId);
            return Result<IssueDto>.Success(result!, 201);
        }

        public async Task<Result> UpdateIssueAsync(long id, UpdateIssueDto dto, long roleId, long currentUserId)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return Result.Failure("Issue title is required.", 400);
            }

            var issue = await _db.Issues
                .Include(i => i.Task)
                .ThenInclude(t => t.Project)
                .FirstOrDefaultAsync(i => i.Id == id && i.IsDeleted != true);

            if (issue == null)
            {
                return Result.Failure($"Issue with ID {id} not found.", 404);
            }

            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            if (!await CanAccessTaskAsync(issue.TaskId, isAdmin, currentUserId))
            {
                return Result.Failure("You do not have access to this task.", 403);
            }

            if (dto.AssignedTo.HasValue && !await IsTaskMemberAsync(issue.Task.ProjectId, dto.AssignedTo.Value))
            {
                return Result.Failure("Issue assignee must belong to the selected project.", 400);
            }

            issue.Title = dto.Title.Trim();
            issue.Description = dto.Description;
            issue.AssignedTo = dto.AssignedTo;
            issue.EstimatedHours = dto.EstimatedHours;
            issue.ActualHours = dto.ActualHours;
            issue.DelayReason = dto.DelayReason;
            issue.IsBlocked = dto.IsBlocked;
            issue.BlockedBy = dto.BlockedBy;
            issue.EscalationLevel = dto.EscalationLevel;
            issue.StartDate = ToUtcDate(dto.StartDate);
            issue.DueDate = ToUtcDate(dto.DueDate);
            issue.StatusId = dto.StatusId;
            issue.PriorityId = dto.PriorityId;
            issue.UpdatedAt = DateTime.UtcNow;
            issue.UpdatedBy = currentUserId;

            _db.Issues.Update(issue);
            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        public async Task<Result> SoftDeleteIssueAsync(long id, long roleId, long currentUserId)
        {
            var issue = await _db.Issues
                .Include(i => i.Task)
                .ThenInclude(t => t.Project)
                .FirstOrDefaultAsync(i => i.Id == id && i.IsDeleted != true);

            if (issue == null)
            {
                return Result.Failure($"Issue with ID {id} not found.", 404);
            }

            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            if (!await CanAccessTaskAsync(issue.TaskId, isAdmin, currentUserId))
            {
                return Result.Failure("You do not have access to this task.", 403);
            }

            issue.IsDeleted = true;
            issue.UpdatedAt = DateTime.UtcNow;
            issue.UpdatedBy = currentUserId;

            _db.Issues.Update(issue);
            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        private IQueryable<IssueEntity> BuildAccessibleIssueQuery(bool isAdmin, bool isManager, long currentUserId)
        {
            var query = _db.Issues
                .Where(i => i.IsDeleted != true && i.Task.IsDeleted != true && i.Task.Project.IsDeleted != true);

            if (isAdmin)
            {
                return query;
            }

            if (isManager)
            {
                return query.Where(i =>
                    i.AssignedTo == currentUserId ||
                    i.CreatedBy == currentUserId ||
                    i.Task.Project.ProjectMembers.Any(pm => pm.UserId == currentUserId));
            }

            return query.Where(i =>
                i.AssignedTo == currentUserId ||
                i.CreatedBy == currentUserId);
        }

        private async Task<bool> CanAccessTaskAsync(long taskId, bool isAdmin, long currentUserId)
        {
            if (isAdmin)
            {
                return await _db.Tasks.AnyAsync(t => t.Id == taskId && t.IsDeleted != true && !t.IsArchived && t.Project.IsDeleted != true);
            }

            return await _db.Tasks.AnyAsync(t =>
                t.Id == taskId &&
                t.IsDeleted != true &&
                !t.IsArchived &&
                t.Project.IsDeleted != true &&
                (t.AssignedTo == currentUserId ||
                 t.CreatedBy == currentUserId ||
                 t.Project.ProjectMembers.Any(pm => pm.UserId == currentUserId)));
        }

        private async Task<bool> IsTaskMemberAsync(long projectId, long userId)
        {
            return await _db.ProjectMembers.AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
        }

        private static System.Linq.Expressions.Expression<Func<IssueEntity, IssueDto>> ToDtoProjection()
        {
            return i => new IssueDto
            {
                Id = i.Id,
                TaskId = i.TaskId,
                TaskTitle = i.Task.Title,
                ProjectId = i.Task.ProjectId,
                ProjectName = i.Task.Project.Name,
                Title = i.Title,
                Description = i.Description,
                AssignedTo = i.AssignedTo,
                AssignedToName = i.AssignedToNavigation != null
                    ? i.AssignedToNavigation.FirstName + " " + i.AssignedToNavigation.LastName
                    : null,
                EstimatedHours = i.EstimatedHours,
                ActualHours = i.ActualHours,
                DelayReason = i.DelayReason,
                IsBlocked = i.IsBlocked,
                BlockedBy = i.BlockedBy,
                EscalationLevel = i.EscalationLevel,
                StartDate = i.StartDate,
                DueDate = i.DueDate,
                StatusId = i.StatusId,
                PriorityId = i.PriorityId,
                CreatedAt = i.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = i.UpdatedAt
            };
        }
    }
}
