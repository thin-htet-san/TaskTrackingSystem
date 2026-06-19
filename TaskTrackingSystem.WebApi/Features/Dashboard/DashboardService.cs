using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Enums;
using TaskTrackingSystem.Shared.Models.Dashboard;

namespace TaskTrackingSystem.WebApi.Features.Dashboard
{
    public class DashboardService
    {
        private readonly AppDbContext _db;

        public DashboardService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<Result<DashboardSummaryDto>> GetSummaryAsync(string roleName, long currentUserId)
        {
            var projects = BuildAccessibleProjectQuery(roleName, currentUserId);
            var tasks = BuildAccessibleTaskQuery(roleName, currentUserId);

            var totalUsers = IsAdmin(roleName)
                ? await _db.Users.CountAsync(u => !u.IsDeleted)
                : await _db.Users
                    .Where(u => !u.IsDeleted &&
                                projects.SelectMany(p => p.ProjectMembers.Select(pm => pm.UserId))
                                    .Distinct()
                                    .Contains(u.Id))
                    .CountAsync();

            var activeProjectsCount = await projects.CountAsync();
            var pendingTasksCount = await tasks.CountAsync(t => t.StatusId != AppTaskStatus.Done);

            var summary = new DashboardSummaryDto
            {
                TotalUsers = totalUsers,
                ActiveProjectsCount = activeProjectsCount,
                PendingTasksCount = pendingTasksCount
            };

            return Result<DashboardSummaryDto>.Success(summary);
        }

        public async Task<Result<IEnumerable<TaskStatusOverviewDto>>> GetTasksOverviewAsync(string roleName, long currentUserId)
        {
            var groupedTasks = await BuildAccessibleTaskQuery(roleName, currentUserId)
                .GroupBy(t => t.StatusId)
                .Select(g => new
                {
                    StatusId = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var statusMap = new Dictionary<AppTaskStatus, string>
            {
                { AppTaskStatus.Todo, "To Do" },
                { AppTaskStatus.InProgress, "In Progress" },
                { AppTaskStatus.Done, "Done" }
            };

            var overview = groupedTasks.Select(gt => new TaskStatusOverviewDto
            {
                StatusId = gt.StatusId,
                StatusName = statusMap.TryGetValue(gt.StatusId, out var name) ? name : $"Status {gt.StatusId}",
                TaskCount = gt.Count
            }).ToList();

            foreach (var status in statusMap)
            {
                if (!overview.Any(o => o.StatusId == status.Key))
                {
                    overview.Add(new TaskStatusOverviewDto
                    {
                        StatusId = status.Key,
                        StatusName = status.Value,
                        TaskCount = 0
                    });
                }
            }

            return Result<IEnumerable<TaskStatusOverviewDto>>.Success(overview.OrderBy(o => o.StatusId));
        }

        public async Task<Result<IEnumerable<ProjectProgressDto>>> GetProjectProgressAsync(string roleName, long currentUserId)
        {
            var activeProjects = await BuildAccessibleProjectQuery(roleName, currentUserId).ToListAsync();
            var progressList = new List<ProjectProgressDto>();

            foreach (var project in activeProjects)
            {
                var tasks = await _db.Tasks
                    .Where(t => t.ProjectId == project.Id && t.IsDeleted != true && !t.IsArchived)
                    .ToListAsync();

                int totalTasks = tasks.Count;
                int completedTasks = tasks.Count(t => t.StatusId == AppTaskStatus.Done);
                double percentage = totalTasks > 0 ? Math.Round(((double)completedTasks / totalTasks) * 100, 2) : 0;

                progressList.Add(new ProjectProgressDto
                {
                    ProjectId = project.Id,
                    ProjectName = project.Name,
                    TotalTasksCount = totalTasks,
                    CompletedTasksCount = completedTasks,
                    CompletionPercentage = percentage
                });
            }

            return Result<IEnumerable<ProjectProgressDto>>.Success(progressList);
        }

        private IQueryable<TaskTrackingSystem.Database.AppDbContextModels.Task> BuildAccessibleTaskQuery(string roleName, long currentUserId)
        {
            var query = _db.Tasks.Where(t => t.IsDeleted != true && !t.IsArchived);

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

        private static bool IsAdmin(string roleName)
        {
            return string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsManager(string roleName)
        {
            return string.Equals(roleName, "Manager", StringComparison.OrdinalIgnoreCase);
        }
    }
}
