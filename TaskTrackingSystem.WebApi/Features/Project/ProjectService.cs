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

namespace TaskTrackingSystem.WebApi.Features.Project
{
    public class ProjectService
    {
        private readonly AppDbContext _db;

        public ProjectService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<ProjectDto>> GetAllProjectsAsync()
        {
            return await _db.Projects
                .Where(p => p.IsDeleted != true)
                .Select(p => new ProjectDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    CreatedById = p.CreatedById,
                    CreatedAt = p.CreatedAt ?? DateTime.UtcNow
                }).ToListAsync();
        }

        public async Task<ProjectDto?> GetProjectByIdAsync(long id)
        {
            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted != true);

            if (project == null) return null;

            return new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                CreatedById = project.CreatedById,
                CreatedAt = project.CreatedAt ?? DateTime.UtcNow
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
                CreatedBy = currentUserId
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
                CreatedAt = project.CreatedAt ?? DateTime.UtcNow
            };

            return Result<ProjectDto>.Success(resultDto, 201);
        }

        public async Task<Result> UpdateProjectAsync(long id, UpdateProjectDto dto, long? currentUserId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Result.Failure(ResultMessages.ProjectNameRequired, 400);
            }

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted != true);
            if (project == null) return Result.Failure(ResultMessages.ProjectNotFound(id), 404);

            project.Name = dto.Name;
            project.Description = dto.Description;
            project.StartDate = dto.StartDate;
            project.EndDate = dto.EndDate;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedBy = currentUserId;

            _db.Projects.Update(project);
            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        public async Task<Result> SoftDeleteProjectAsync(long id)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted != true);
            if (project == null) return Result.Failure(ResultMessages.ProjectNotFound(id), 404);

            project.IsDeleted = true;
            _db.Projects.Update(project);
            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        public async Task<Result<IEnumerable<UserDto>>> GetProjectMembersAsync(long projectId)
        {
            var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId && p.IsDeleted != true);
            if (!projectExists)
            {
                return Result<IEnumerable<UserDto>>.Failure(ResultMessages.ProjectNotFound(projectId), 404);
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

        public async Task<Result> AssignMembersToProjectAsync(long projectId, AssignMembersDto dto)
        {
            var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId && p.IsDeleted != true);
            if (!projectExists)
            {
                return Result.Failure(ResultMessages.ProjectNotFound(projectId), 404);
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
            return Result.Success(200);
        }

        public async Task<Result> RemoveMemberFromProjectAsync(long projectId, long userId)
        {
            var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId && p.IsDeleted != true);
            if (!projectExists)
            {
                return Result.Failure(ResultMessages.ProjectNotFound(projectId), 404);
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

        public async Task<Result<IEnumerable<TaskDto>>> GetProjectTasksAsync(long projectId)
        {
            var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId && p.IsDeleted != true);
            if (!projectExists)
            {
                return Result<IEnumerable<TaskDto>>.Failure(ResultMessages.ProjectNotFound(projectId), 404);
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
                    CreatedAt = t.CreatedAt ?? DateTime.UtcNow
                })
                .ToListAsync();

            return Result<IEnumerable<TaskDto>>.Success(tasks);
        }
    }
}
