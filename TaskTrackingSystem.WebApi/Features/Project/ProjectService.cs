using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Enums;
using TaskTrackingSystem.Shared.Models.Project;
using TaskTrackingSystem.Shared.Models.Task;
using TaskTrackingSystem.Shared.Models.User;
using TaskTrackingSystem.WebApi.Infrastructure;

namespace TaskTrackingSystem.WebApi.Features.Project
{
    public class ProjectService
    {
        private readonly AppDbContext _db;
        private readonly TaskTrackingSystem.WebApi.Features.Notification.FirebaseNotificationService _notificationService;

        public ProjectService(AppDbContext db, TaskTrackingSystem.WebApi.Features.Notification.FirebaseNotificationService notificationService)
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

        private static DateTime ToUtcDate(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
        }

        public async Task<IEnumerable<ProjectDto>> GetAllProjectsAsync(long roleId, long currentUserId)
        {
            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            return await BuildAccessibleProjectQuery(isAdmin, currentUserId)
                .OrderBy(p => p.EndDate)
                .ThenByDescending(p => p.CreatedAt ?? DateTime.UtcNow)
                .Select(p => new ProjectDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    CreatedById = p.CreatedById,
                    CreatedAt = p.CreatedAt ?? DateTime.UtcNow,
                    BudgetedHours = p.BudgetedHours
                }).ToListAsync();
        }

        public async Task<PagedResult<ProjectDto>> GetPagedProjectsAsync(long roleId, long currentUserId, string? search, int page, int pageSize)
        {
            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            var query = BuildAccessibleProjectQuery(isAdmin, currentUserId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                query = query.Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(searchTerm)) ||
                    (p.Description != null && p.Description.ToLower().Contains(searchTerm)));
            }

            return await query
                .OrderBy(p => p.EndDate)
                .ThenByDescending(p => p.CreatedAt ?? DateTime.UtcNow)
                .Select(p => new ProjectDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    CreatedById = p.CreatedById,
                    CreatedAt = p.CreatedAt ?? DateTime.UtcNow,
                    BudgetedHours = p.BudgetedHours
                })
                .ToPagedResultAsync(page, pageSize);
        }

        public async Task<ProjectDto?> GetProjectByIdAsync(long id, long roleId, long currentUserId)
        {
            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            var project = await BuildAccessibleProjectQuery(isAdmin, currentUserId)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                return null;
            }

            return new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                CreatedById = project.CreatedById,
                CreatedAt = project.CreatedAt ?? DateTime.UtcNow,
                BudgetedHours = project.BudgetedHours
            };
        }

        public async Task<Result<ProjectDto>> CreateProjectAsync(CreateProjectDto dto, long? currentUserId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Result<ProjectDto>.Failure(ResultMessages.ProjectNameRequired, 400);
            }

            var project = new TaskTrackingSystem.Database.AppDbContextModels.Project
            {
                Name = dto.Name,
                Description = dto.Description,
                StartDate = ToUtcDate(dto.StartDate),
                EndDate = ToUtcDate(dto.EndDate),
                CreatedById = dto.CreatedById,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserId,
                BudgetedHours = dto.BudgetedHours
            };

            _db.Projects.Add(project);
            await _db.SaveChangesAsync();

            var resultDto = new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                CreatedById = project.CreatedById,
                CreatedAt = project.CreatedAt ?? DateTime.UtcNow,
                BudgetedHours = project.BudgetedHours
            };

            return Result<ProjectDto>.Success(resultDto, 201);
        }

        public async Task<Result> UpdateProjectAsync(long id, UpdateProjectDto dto, long roleId, long currentUserId)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Result.Failure(ResultMessages.ProjectNameRequired, 400);
            }

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted != true);
            if (project == null)
            {
                return Result.Failure(ResultMessages.ProjectNotFound(id), 404);
            }

            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            if (!await IsProjectAccessibleAsync(id, isAdmin, currentUserId))
            {
                return Result.Failure("You do not have access to this project.", 403);
            }

            project.Name = dto.Name;
            project.Description = dto.Description;
            project.StartDate = ToUtcDate(dto.StartDate);
            project.EndDate = ToUtcDate(dto.EndDate);
            project.BudgetedHours = dto.BudgetedHours;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedBy = currentUserId;

            _db.Projects.Update(project);
            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        public async Task<Result> SoftDeleteProjectAsync(long id, long roleId, long currentUserId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted != true);
            if (project == null)
            {
                return Result.Failure(ResultMessages.ProjectNotFound(id), 404);
            }

            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            if (!await IsProjectAccessibleAsync(id, isAdmin, currentUserId))
            {
                return Result.Failure("You do not have access to this project.", 403);
            }

            project.IsDeleted = true;
            _db.Projects.Update(project);

            var tasksToUpdate = await _db.Tasks
                .Where(t => t.ProjectId == id && t.IsDeleted != true)
                .ToListAsync();
            foreach (var task in tasksToUpdate)
            {
                task.IsDeleted = true;
                task.UpdatedAt = DateTime.UtcNow;
                task.UpdatedBy = currentUserId;
            }

            if (tasksToUpdate.Any())
            {
                _db.Tasks.UpdateRange(tasksToUpdate);
            }

            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        public async Task<Result<IEnumerable<UserDto>>> GetProjectMembersAsync(long projectId, long roleId, long currentUserId)
        {
            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            if (!await IsProjectAccessibleAsync(projectId, isAdmin, currentUserId))
            {
                return Result<IEnumerable<UserDto>>.Failure("You do not have access to this project.", 403);
            }

            var members = await _db.ProjectMembers
                .Where(pm => pm.ProjectId == projectId)
                .Join(_db.Users.Where(u => !u.IsDeleted),
                      pm => pm.UserId,
                      u => u.Id,
                      (pm, u) => new UserDto
                      {
                          Id = u.Id,
                          Username = u.Username,
                          FirstName = u.FirstName,
                          LastName = u.LastName,
                          Email = u.Email,
                          Phone = u.Phone,
                          RoleId = u.RoleId,
                          IsActive = u.IsActive,
                          CreatedAt = u.CreatedAt ?? DateTime.UtcNow
                      })
                .ToListAsync();

            return Result<IEnumerable<UserDto>>.Success(members);
        }

        public async Task<Result> AssignMembersToProjectAsync(long projectId, AssignMembersDto dto, long roleId, long currentUserId)
        {
            var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId && p.IsDeleted != true);
            if (!projectExists)
            {
                return Result.Failure(ResultMessages.ProjectNotFound(projectId), 404);
            }

            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            if (!await IsProjectAccessibleAsync(projectId, isAdmin, currentUserId))
            {
                return Result.Failure("You do not have access to this project.", 403);
            }

            if (dto.UserIds == null)
            {
                return Result.Failure(ResultMessages.UserIdsCannotBeNull, 400);
            }

            if (dto.UserIds.Any())
            {
                var validUserIds = await _db.Users
                    .Where(u => dto.UserIds.Contains(u.Id) && !u.IsDeleted)
                    .Select(u => u.Id)
                    .ToListAsync();

                var invalidUserIds = dto.UserIds.Except(validUserIds).ToList();
                if (invalidUserIds.Any())
                {
                    return Result.Failure(ResultMessages.InvalidUserIds(string.Join(", ", invalidUserIds)), 400);
                }
            }

            var existingMembers = await _db.ProjectMembers
                .Where(pm => pm.ProjectId == projectId)
                .ToListAsync();
            var previousMemberIds = existingMembers.Select(pm => pm.UserId).ToHashSet();

            if (existingMembers.Any())
            {
                _db.ProjectMembers.RemoveRange(existingMembers);
            }

            foreach (var userId in dto.UserIds)
            {
                _db.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = projectId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();

            var addedMemberIds = dto.UserIds.Except(previousMemberIds).ToList();
            if (addedMemberIds.Count > 0)
            {
                var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.IsDeleted != true);
                if (project != null)
                {
                    await _notificationService.NotifyProjectAssignedAsync(project, addedMemberIds, currentUserId);
                }
            }

            return Result.Success(200);
        }

        public async Task<Result> RemoveMemberFromProjectAsync(long projectId, long userId, long roleId, long currentUserId)
        {
            var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId && p.IsDeleted != true);
            if (!projectExists)
            {
                return Result.Failure(ResultMessages.ProjectNotFound(projectId), 404);
            }

            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            if (!await IsProjectAccessibleAsync(projectId, isAdmin, currentUserId))
            {
                return Result.Failure("You do not have access to this project.", 403);
            }

            var member = await _db.ProjectMembers
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);

            if (member == null)
            {
                return Result.Failure(ResultMessages.UserNotProjectMember(userId, projectId), 404);
            }

            _db.ProjectMembers.Remove(member);
            await _db.SaveChangesAsync();

            return Result.Success(204);
        }

        public async Task<Result<IEnumerable<TaskDto>>> GetProjectTasksAsync(long projectId, long roleId, long currentUserId)
        {
            var isAdmin = await DataScopeAuthorization.IsAdminScopeAsync(_db, roleId);
            if (!await IsProjectAccessibleAsync(projectId, isAdmin, currentUserId))
            {
                return Result<IEnumerable<TaskDto>>.Failure("You do not have access to this project.", 403);
            }

            var tasks = await _db.Tasks
                .Where(t => t.ProjectId == projectId && t.IsDeleted != true && !t.IsArchived)
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

        private IQueryable<TaskTrackingSystem.Database.AppDbContextModels.Project> BuildAccessibleProjectQuery(bool isAdmin, long currentUserId)
        {
            var query = _db.Projects.Where(p => p.IsDeleted != true);

            if (isAdmin)
            {
                return query;
            }

            return query.Where(p =>
                p.CreatedById == currentUserId ||
                p.ProjectMembers.Any(pm => pm.UserId == currentUserId));
        }

        private async Task<bool> IsProjectAccessibleAsync(long projectId, bool isAdmin, long currentUserId)
        {
            if (isAdmin)
            {
                return await _db.Projects.AnyAsync(p => p.Id == projectId && p.IsDeleted != true);
            }

            return await _db.Projects.AnyAsync(p =>
                p.Id == projectId &&
                p.IsDeleted != true &&
                (p.CreatedById == currentUserId || p.ProjectMembers.Any(pm => pm.UserId == currentUserId)));
        }
    }
}
