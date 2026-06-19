using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.User;
using TaskTrackingSystem.Shared.Models.Task;
using TaskTrackingSystem.Shared.Models.Project;
using TaskTrackingSystem.Shared.Enums;

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

        public async Task<IEnumerable<ProjectDto>> GetAllProjectsAsync(string roleName, long currentUserId)
        {
            return await BuildAccessibleProjectQuery(roleName, currentUserId)
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

        public async Task<ProjectDto?> GetProjectByIdAsync(long id, string roleName, long currentUserId)
        {
            var project = await BuildAccessibleProjectQuery(roleName, currentUserId)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null) return null;

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
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
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

        public async Task<Result> UpdateProjectAsync(long id, UpdateProjectDto dto, string roleName, long currentUserId)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Result.Failure(ResultMessages.ProjectNameRequired, 400);
            }

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted != true);
            if (project == null) return Result.Failure(ResultMessages.ProjectNotFound(id), 404);

            if (!await IsProjectAccessibleAsync(id, roleName, currentUserId))
            {
                return Result.Failure("You do not have access to this project.", 403);
            }

            project.Name = dto.Name;
            project.Description = dto.Description;
            project.StartDate = dto.StartDate;
            project.EndDate = dto.EndDate;
            project.BudgetedHours = dto.BudgetedHours;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedBy = currentUserId;

            _db.Projects.Update(project);
            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        public async Task<Result> SoftDeleteProjectAsync(long id, string roleName, long currentUserId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted != true);
            if (project == null) return Result.Failure(ResultMessages.ProjectNotFound(id), 404);

            if (!await IsProjectAccessibleAsync(id, roleName, currentUserId))
            {
                return Result.Failure("You do not have access to this project.", 403);
            }

            project.IsDeleted = true;
            _db.Projects.Update(project);

            // Soft-delete all tasks associated with this project
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

        public async Task<Result<IEnumerable<UserDto>>> GetProjectMembersAsync(long projectId, string roleName, long currentUserId)
        {
            if (!await IsProjectAccessibleAsync(projectId, roleName, currentUserId))
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

        public async Task<Result> AssignMembersToProjectAsync(long projectId, AssignMembersDto dto, string roleName, long currentUserId)
        {
            var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId && p.IsDeleted != true);
            if (!projectExists)
            {
                return Result.Failure(ResultMessages.ProjectNotFound(projectId), 404);
            }

            if (!await IsProjectAccessibleAsync(projectId, roleName, currentUserId))
            {
                return Result.Failure("You do not have access to this project.", 403);
            }

            if (dto.UserIds == null)
            {
                return Result.Failure(ResultMessages.UserIdsCannotBeNull, 400);
            }

            // Verify if user IDs are valid and not deleted
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

            // Purge old assignments
            var existingMembers = await _db.ProjectMembers
                .Where(pm => pm.ProjectId == projectId)
                .ToListAsync();
            var previousMemberIds = existingMembers.Select(pm => pm.UserId).ToHashSet();

            if (existingMembers.Any())
            {
                _db.ProjectMembers.RemoveRange(existingMembers);
            }

            // Bulk insert new assignments
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

        public async Task<Result> RemoveMemberFromProjectAsync(long projectId, long userId, string roleName, long currentUserId)
        {
            var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId && p.IsDeleted != true);
            if (!projectExists)
            {
                return Result.Failure(ResultMessages.ProjectNotFound(projectId), 404);
            }

            if (!await IsProjectAccessibleAsync(projectId, roleName, currentUserId))
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

            return Result.Success(204); // No Content success
        }

        public async Task<Result<IEnumerable<TaskDto>>> GetProjectTasksAsync(long projectId, string roleName, long currentUserId)
        {
            if (!await IsProjectAccessibleAsync(projectId, roleName, currentUserId))
            {
                return Result<IEnumerable<TaskDto>>.Failure("You do not have access to this project.", 403);
            }

            var tasks = await _db.Tasks
                .Where(t => t.ProjectId == projectId && t.IsDeleted != true)
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
                    EstimatedHours = t.EstimatedHours,
                    DueDate = t.DueDate,
                    CreatedAt = t.CreatedAt ?? DateTime.UtcNow,
                    CompletedAt = t.TaskHistories
                        .Where(th => th.NewStatusId == AppTaskStatus.Done)
                        .OrderBy(th => th.CreatedAt)
                        .Select(th => th.CreatedAt)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Result<IEnumerable<TaskDto>>.Success(tasks);
        }

        private IQueryable<TaskTrackingSystem.Database.AppDbContextModels.Project> BuildAccessibleProjectQuery(string roleName, long currentUserId)
        {
            var query = _db.Projects.Where(p => p.IsDeleted != true);

            if (IsAdmin(roleName))
            {
                return query;
            }

            return query.Where(p =>
                p.CreatedById == currentUserId ||
                p.ProjectMembers.Any(pm => pm.UserId == currentUserId));
        }

        private async Task<bool> IsProjectAccessibleAsync(long projectId, string roleName, long currentUserId)
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

        private static bool IsAdmin(string roleName)
        {
            return roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
