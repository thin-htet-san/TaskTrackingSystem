using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared.Models.Task;

using TaskTrackingSystem.Shared;

namespace TaskTrackingSystem.WebApi.Features.Task
{
    public class TaskService
    {
        private readonly AppDbContext _db;

        public TaskService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<TaskDto>> GetAllTasksAsync()
        {
            return await _db.Tasks
                .Where(t => t.IsDeleted != true)
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
                }).ToListAsync();
        }

        public async Task<TaskDto?> GetTaskByIdAsync(long id)
        {
            var task = await _db.Tasks
                .FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted != true);

            if (task == null) return null;

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
                EstimatedHours = task.EstimatedHours,
                DueDate = task.DueDate,
                CreatedAt = task.CreatedAt ?? DateTime.UtcNow
            };
        }

        public async Task<Result<TaskDto>> CreateTaskAsync(CreateTaskDto dto, long? currentUserId = null)
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

            var task = new TaskTrackingSystem.Database.AppDbContextModels.Task
            {
                Title = dto.Title,
                Description = dto.Description,
                ProjectId = dto.ProjectId,
                StatusId = dto.StatusId == 0 ? 1 : dto.StatusId,
                PriorityId = dto.PriorityId == 0 ? 2 : dto.PriorityId,
                AssignedTo = dto.AssignedTo,
                AssignedBy = dto.AssignedBy,
                EstimatedHours = dto.EstimatedHours,
                DueDate = dto.DueDate,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserId
            };

            _db.Tasks.Add(task);
            await _db.SaveChangesAsync();

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
                EstimatedHours = task.EstimatedHours,
                DueDate = task.DueDate,
                CreatedAt = task.CreatedAt ?? DateTime.UtcNow
            };

            return Result<TaskDto>.Success(resultDto, 201);
        }

        public async Task<Result> UpdateTaskAsync(long id, UpdateTaskDto dto, long? currentUserId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return Result.Failure(ResultMessages.TaskTitleRequired, 400);
            }

            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted != true);
            if (task == null) return Result.Failure(ResultMessages.TaskNotFound(id), 404);

            task.Title = dto.Title;
            task.Description = dto.Description;
            task.StatusId = dto.StatusId;
            task.PriorityId = dto.PriorityId;
            task.AssignedTo = dto.AssignedTo;
            task.AssignedBy = dto.AssignedBy;
            task.EstimatedHours = dto.EstimatedHours;
            task.DueDate = dto.DueDate;
            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedBy = currentUserId;

            _db.Tasks.Update(task);
            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        public async Task<Result> SoftDeleteTaskAsync(long id)
        {
            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted != true);
            if (task == null) return Result.Failure(ResultMessages.TaskNotFound(id), 404);

            task.IsDeleted = true;
            _db.Tasks.Update(task);
            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        public async Task<Result<IEnumerable<TaskDto>>> GetTasksByUserIdAsync(long userId)
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted);
            if (!userExists)
            {
                return Result<IEnumerable<TaskDto>>.Failure(ResultMessages.UserNotFound(userId), 404);
            }

            var tasks = await _db.Tasks
                .Where(t => t.AssignedTo == userId && t.IsDeleted != true)
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

        public async Task<Result> UpdateTaskStatusAsync(long id, long statusId, long? currentUserId = null)
        {
            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted != true);
            if (task == null)
            {
                return Result.Failure(ResultMessages.TaskNotFound(id), 404);
            }

            task.StatusId = statusId;
            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedBy = currentUserId;

            _db.Tasks.Update(task);
            await _db.SaveChangesAsync();

            return Result.Success(200);
        }
    }
}
