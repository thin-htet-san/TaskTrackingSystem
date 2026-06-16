using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
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
            var elevated = IsElevated(roleName);
            var projects = BuildAccessibleProjectQuery(roleName, currentUserId);
            var tasks = BuildAccessibleTaskQuery(roleName, currentUserId);

            var totalUsers = elevated
                ? await _db.Users.CountAsync(u => !u.IsDeleted)
                : await _db.Users.CountAsync(u => u.Id == currentUserId && !u.IsDeleted);

            var activeProjectsCount = await projects.CountAsync();
            var pendingTasksCount = await tasks.CountAsync(t => t.StatusId != 3);

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

            // Status mappings (StatusId 1 = To Do, 2 = In Progress, 3 = Done etc.)
            var statusMap = new Dictionary<long, string>
            {
                { 1, "To Do" },
                { 2, "In Progress" },
                { 3, "Done" }
            };

            var overview = groupedTasks.Select(gt => new TaskStatusOverviewDto
            {
                StatusId = gt.StatusId,
                StatusName = statusMap.TryGetValue(gt.StatusId, out var name) ? name : $"Status {gt.StatusId}",
                TaskCount = gt.Count
            }).ToList();

            // Make sure standard statuses are represented even if count is 0
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
            var activeProjects = await BuildAccessibleProjectQuery(roleName, currentUserId)
                .ToListAsync();

            var progressList = new List<ProjectProgressDto>();

            foreach (var project in activeProjects)
            {
                var tasks = await _db.Tasks
                    .Where(t => t.ProjectId == project.Id && t.IsDeleted != true)
                    .ToListAsync();

                int totalTasks = tasks.Count;
                int completedTasks = tasks.Count(t => t.StatusId == 3); // StatusId 3 = Done

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
            var query = _db.Tasks.Where(t => t.IsDeleted != true);

            if (IsElevated(roleName))
            {
                return query;
            }

            return query.Where(t =>
                t.AssignedTo == currentUserId ||
                t.CreatedBy == currentUserId);
        }

        private IQueryable<TaskTrackingSystem.Database.AppDbContextModels.Project> BuildAccessibleProjectQuery(string roleName, long currentUserId)
        {
            var query = _db.Projects.Where(p => p.IsDeleted != true);

            if (IsElevated(roleName))
            {
                return query;
            }

            return query.Where(p =>
                p.CreatedById == currentUserId ||
                p.ProjectMembers.Any(pm => pm.UserId == currentUserId));
        }

        private static bool IsElevated(string roleName)
        {
            return roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                   roleName.Equals("Manager", StringComparison.OrdinalIgnoreCase);
        }
    }
}
