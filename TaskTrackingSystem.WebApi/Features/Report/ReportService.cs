using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Report;
using TaskTrackingSystem.Shared.Enums;
using TaskTrackingSystem.WebApi.Infrastructure;

namespace TaskTrackingSystem.WebApi.Features.Report
{
    public class ReportService
    {
        private readonly AppDbContext _db;
        private static readonly Dictionary<AppTaskStatus, string> StatusMap = new() { { AppTaskStatus.Todo, "To Do" }, { AppTaskStatus.InProgress, "In Progress" }, { AppTaskStatus.Done, "Done" } };
        private static readonly Dictionary<TaskPriority, string> PriorityMap = new() { { TaskPriority.Low, "Low" }, { TaskPriority.Medium, "Medium" }, { TaskPriority.High, "High" } };

        public ReportService(AppDbContext db)
        {
            _db = db;
        }

        private static bool IsAdmin(string roleName)
        {
            return roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        private IQueryable<TaskTrackingSystem.Database.AppDbContextModels.Task> BuildAccessibleTaskQuery(string roleName, long currentUserId)
        {
            var query = _db.Tasks.Where(t => t.IsDeleted != true && !t.IsArchived);

            if (IsAdmin(roleName))
            {
                return query;
            }

            if (roleName.Equals("Manager", StringComparison.OrdinalIgnoreCase))
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

        private IQueryable<TaskTrackingSystem.Database.AppDbContextModels.User> BuildAccessibleUserQuery(string roleName, long currentUserId)
        {
            var users = _db.Users.Where(u => !u.IsDeleted);

            if (IsAdmin(roleName))
            {
                return users;
            }

            var accessibleUserIds = BuildAccessibleProjectQuery(roleName, currentUserId)
                .SelectMany(p => p.ProjectMembers.Select(pm => pm.UserId))
                .Distinct();

            return users.Where(u => u.Id == currentUserId || accessibleUserIds.Contains(u.Id));
        }

        private static PagedResult<TDestination> MapPagedResult<TSource, TDestination>(
            PagedResult<TSource> source,
            Func<TSource, TDestination> map)
        {
            return new PagedResult<TDestination>
            {
                Items = source.Items.Select(map).ToList(),
                TotalCount = source.TotalCount,
                Page = source.Page,
                PageSize = source.PageSize,
                TotalPages = source.TotalPages
            };
        }

        private IQueryable<TaskTrackingSystem.Database.AppDbContextModels.Task> BuildTaskReportQuery(
            string roleName,
            long currentUserId,
            DateTime? startDate,
            DateTime? endDate,
            string? status,
            int? projectId)
        {
            var query = BuildAccessibleTaskQuery(roleName, currentUserId)
                .Include(t => t.Project)
                .Include(t => t.AssignedToNavigation)
                .Include(t => t.AssignedByNavigation)
                .Where(t => t.IsDeleted != true);

            if (startDate.HasValue)
                query = query.Where(t => t.CreatedAt >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(t => t.CreatedAt < endDate.Value.Date.AddDays(1));

            if (projectId.HasValue)
                query = query.Where(t => t.ProjectId == projectId.Value);

            if (!string.IsNullOrWhiteSpace(status))
            {
                var sl = status.Trim().ToLower();
                if (sl == "uncompleted")
                {
                    query = query.Where(t => t.StatusId != AppTaskStatus.Done);
                }
                else if (sl == "overdue")
                {
                    query = query.Where(t => t.DueDate < DateTime.Today && t.StatusId != AppTaskStatus.Done);
                }
                else
                {
                    AppTaskStatus? sid = sl switch
                    {
                        "to do" => AppTaskStatus.Todo,
                        "in progress" => AppTaskStatus.InProgress,
                        "done" => AppTaskStatus.Done,
                        _ => null
                    };

                    if (sid.HasValue)
                        query = query.Where(t => t.StatusId == sid.Value);
                    else if (Enum.TryParse<AppTaskStatus>(status, true, out var parsedStatus))
                        query = query.Where(t => t.StatusId == parsedStatus);
                }
            }

            return query;
        }

        private static TaskReportDto MapTaskReport(TaskTrackingSystem.Database.AppDbContextModels.Task t)
        {
            return new TaskReportDto
            {
                TaskId = t.Id,
                Title = t.Title,
                Description = t.Description,
                ProjectId = t.ProjectId,
                ProjectName = t.Project?.Name ?? string.Empty,
                StatusId = t.StatusId,
                StatusName = StatusMap.TryGetValue(t.StatusId, out var s) ? s : $"Status {t.StatusId}",
                PriorityId = t.PriorityId,
                PriorityName = PriorityMap.TryGetValue(t.PriorityId, out var p) ? p : $"Priority {t.PriorityId}",
                AssignedToUserId = t.AssignedTo,
                AssignedToUser = t.AssignedToNavigation != null ? $"{t.AssignedToNavigation.FirstName} {t.AssignedToNavigation.LastName}" : null,
                AssignedByUser = t.AssignedByNavigation != null ? $"{t.AssignedByNavigation.FirstName} {t.AssignedByNavigation.LastName}" : null,
                EstimatedHours = t.EstimatedHours,
                DueDate = t.DueDate,
                CreatedAt = t.CreatedAt ?? DateTime.UtcNow
            };
        }

        // â”€â”€â”€ Legacy endpoints (kept for backward compatibility) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<Result<IEnumerable<TaskReportDto>>> GetTasksReportAsync(
            DateTime? startDate, DateTime? endDate, string? status, int? projectId, string roleName, long currentUserId)
        {
            var tasks = await BuildTaskReportQuery(roleName, currentUserId, startDate, endDate, status, projectId)
                .OrderBy(t => t.DueDate)
                .ThenByDescending(t => t.CreatedAt ?? DateTime.UtcNow)
                .ToListAsync();
            var list = tasks.Select(MapTaskReport).ToList();
            return Result<IEnumerable<TaskReportDto>>.Success(list);
        }

        public async Task<PagedResult<TaskReportDto>> GetPagedTasksReportAsync(
            DateTime? startDate,
            DateTime? endDate,
            string? status,
            int? projectId,
            string roleName,
            long currentUserId,
            int page,
            int pageSize)
        {
            var paged = await BuildTaskReportQuery(roleName, currentUserId, startDate, endDate, status, projectId)
                .OrderBy(t => t.DueDate)
                .ThenByDescending(t => t.CreatedAt ?? DateTime.UtcNow)
                .ToPagedResultAsync(page, pageSize);

            return MapPagedResult(paged, MapTaskReport);
        }

        public async Task<Result<IEnumerable<UserProductivityDto>>> GetUserProductivityReportAsync(string roleName, long currentUserId)
        {
            var users = await BuildAccessibleUserQuery(roleName, currentUserId).ToListAsync();
            var tasks = await BuildAccessibleTaskQuery(roleName, currentUserId)
                .Include(t => t.TaskHistories)
                .ToListAsync();
            var list = new List<UserProductivityDto>();
            foreach (var user in users)
            {
                var userTasks = tasks.Where(t => t.AssignedTo == user.Id).ToList();
                
                int total = userTasks.Count;
                var completedTasks = userTasks.Where(t => t.StatusId == AppTaskStatus.Done).ToList();
                int done = completedTasks.Count;
                
                int onTimeCount = completedTasks.Count(t => 
                    t.TaskHistories.Any(th => th.NewStatusId == AppTaskStatus.Done && th.CreatedAt <= t.DueDate)
                );
                
                double onTimeDeliveryRate = done > 0 ? Math.Round(((double)onTimeCount / done) * 100, 2) : 0;

                list.Add(new UserProductivityDto
                {
                    UserId = user.Id,
                    Username = user.Username,
                    FullName = $"{user.FirstName} {user.LastName}".Trim(),
                    TotalAssignedTasks = total,
                    CompletedTasksCount = done,
                    EfficiencyRatio = total > 0 ? Math.Round(((double)done / total) * 100, 2) : 0,
                    OnTimeDeliveryRate = onTimeDeliveryRate
                });
            }
            return Result<IEnumerable<UserProductivityDto>>.Success(list);
        }

        // â”€â”€â”€ Report 1: Task Status Summary â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<Result<IEnumerable<TaskStatusSummaryDto>>> GetTaskStatusSummaryAsync(
            string? search, AppTaskStatus? statusId, long? projectId, string roleName, long currentUserId)
        {
            var query = BuildAccessibleTaskQuery(roleName, currentUserId)
                .Include(t => t.Project)
                .Include(t => t.AssignedToNavigation)
                .Where(t => t.IsDeleted != true);

            if (statusId.HasValue && Convert.ToInt64(statusId.Value) > 0)
                query = query.Where(t => t.StatusId == statusId.Value);
            if (projectId.HasValue && projectId > 0)
                query = query.Where(t => t.ProjectId == projectId.Value);

            var tasks = await query.OrderBy(t => t.DueDate).ThenByDescending(t => t.CreatedAt ?? DateTime.UtcNow).ToListAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                tasks = tasks.Where(t =>
                    t.Title.ToLower().Contains(s) ||
                    (t.Project?.Name.ToLower().Contains(s) == true) ||
                    (t.AssignedToNavigation != null &&
                     ($"{t.AssignedToNavigation.FirstName} {t.AssignedToNavigation.LastName}").ToLower().Contains(s))
                ).ToList();
            }

            var list = tasks.Select(t => new TaskStatusSummaryDto
            {
                TaskId = t.Id,
                Title = t.Title,
                ProjectName = t.Project?.Name ?? "-",
                StatusName = StatusMap.TryGetValue(t.StatusId, out var s) ? s : $"Status {t.StatusId}",
                PriorityName = PriorityMap.TryGetValue(t.PriorityId, out var p) ? p : $"Priority {t.PriorityId}",
                AssignedTo = t.AssignedToNavigation != null ? $"{t.AssignedToNavigation.FirstName} {t.AssignedToNavigation.LastName}" : null,
                DueDate = t.DueDate,
                CreatedAt = t.CreatedAt ?? DateTime.UtcNow
            }).ToList();

            return Result<IEnumerable<TaskStatusSummaryDto>>.Success(list);
        }

        public byte[] ExportTaskStatusSummaryToExcel(IEnumerable<TaskStatusSummaryDto> data)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Task Status Summary");
            var headers = new[] { "Task ID", "Title", "Project", "Status", "Priority", "Assigned To", "Due Date", "Created At", "Overdue" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#6D28D9");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }
            int row = 2;
            foreach (var item in data)
            {
                ws.Cell(row, 1).Value = item.TaskId;
                ws.Cell(row, 2).Value = item.Title;
                ws.Cell(row, 3).Value = item.ProjectName;
                ws.Cell(row, 4).Value = item.StatusName;
                ws.Cell(row, 5).Value = item.PriorityName;
                ws.Cell(row, 6).Value = item.AssignedTo ?? "Unassigned";
                ws.Cell(row, 7).Value = DisplayFormats.Date(item.DueDate);
                ws.Cell(row, 8).Value = DisplayFormats.Date(item.CreatedAt);
                ws.Cell(row, 9).Value = item.IsOverdue ? "Yes" : "No";
                if (item.IsOverdue) ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2");
                row++;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        public async Task<PagedResult<TeamProductivityReportDto>> GetPagedTeamProductivityAsync(
            string? search,
            string roleName,
            long currentUserId,
            int page,
            int pageSize)
        {
            var full = await GetTeamProductivityAsync(search, roleName, currentUserId);
            if (!full.IsSuccess || full.Value == null)
            {
                return new PagedResult<TeamProductivityReportDto>();
            }

            var ordered = full.Value.OrderByDescending(u => u.CompletionRate).ToList();
            var normalizedPageSize = PaginationExtensions.NormalizePageSize(pageSize);
            var totalCount = ordered.Count;
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
            var normalizedPage = Math.Max(page, 1);
            if (totalPages > 0 && normalizedPage > totalPages)
                normalizedPage = totalPages;

            var items = ordered
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToList();

            return new PagedResult<TeamProductivityReportDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalPages = totalPages
            };
        }

        // â”€â”€â”€ Report 2: Team Productivity â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<Result<IEnumerable<TeamProductivityReportDto>>> GetTeamProductivityAsync(string? search, string roleName, long currentUserId)
        {
            var users = await BuildAccessibleUserQuery(roleName, currentUserId).ToListAsync();
            var allTasks = await BuildAccessibleTaskQuery(roleName, currentUserId)
                .Where(t => t.AssignedTo != null)
                .ToListAsync();
            var now = DateTime.UtcNow;

            var list = users.Select(u =>
            {
                var uTasks = allTasks.Where(t => t.AssignedTo == u.Id).ToList();
                return new TeamProductivityReportDto
                {
                    UserId = u.Id,
                    FullName = $"{u.FirstName} {u.LastName}".Trim(),
                    Username = u.Username,
                    TotalAssigned = uTasks.Count,
                    Completed = uTasks.Count(t => t.StatusId == AppTaskStatus.Done),
                    InProgress = uTasks.Count(t => t.StatusId == AppTaskStatus.InProgress),
                    ToDo = uTasks.Count(t => t.StatusId == AppTaskStatus.Todo),
                    Overdue = uTasks.Count(t => t.DueDate < now && t.StatusId != AppTaskStatus.Done),
                    CompletionRate = uTasks.Count > 0 ? Math.Round((double)uTasks.Count(t => t.StatusId == AppTaskStatus.Done) / uTasks.Count * 100, 1) : 0
                };
            }).ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                list = list.Where(u => u.FullName.ToLower().Contains(s) || u.Username.ToLower().Contains(s)).ToList();
            }

            return Result<IEnumerable<TeamProductivityReportDto>>.Success(list.OrderByDescending(u => u.CompletionRate));
        }

        public byte[] ExportTeamProductivityToExcel(IEnumerable<TeamProductivityReportDto> data)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Team Productivity");
            var headers = new[] { "User ID", "Full Name", "Username", "Total Assigned", "Completed", "In Progress", "To Do", "Overdue", "Completion Rate (%)" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#059669");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }
            int row = 2;
            foreach (var item in data)
            {
                ws.Cell(row, 1).Value = item.UserId;
                ws.Cell(row, 2).Value = item.FullName;
                ws.Cell(row, 3).Value = item.Username;
                ws.Cell(row, 4).Value = item.TotalAssigned;
                ws.Cell(row, 5).Value = item.Completed;
                ws.Cell(row, 6).Value = item.InProgress;
                ws.Cell(row, 7).Value = item.ToDo;
                ws.Cell(row, 8).Value = item.Overdue;
                ws.Cell(row, 9).Value = item.CompletionRate;
                if (item.Overdue > 0) ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7");
                row++;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // â”€â”€â”€ Report 3: Overdue & Critical Tasks â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<Result<IEnumerable<OverdueCriticalTaskDto>>> GetOverdueCriticalTasksAsync(
            string? search,
            long? projectId,
            string roleName,
            long currentUserId,
            int? priorityId = null,
            string? delayType = null)
        {
            var now = DateTime.UtcNow;
            var today = DateTime.Today;
            var query = BuildAccessibleTaskQuery(roleName, currentUserId)
                .Include(t => t.Project)
                .Include(t => t.AssignedToNavigation)
                .Where(t => t.IsDeleted != true && t.StatusId != AppTaskStatus.Done &&
                            (t.DueDate < now || t.PriorityId == TaskPriority.High)); // overdue OR high priority

            if (projectId.HasValue && projectId > 0)
                query = query.Where(t => t.ProjectId == projectId.Value);

            if (priorityId.HasValue && priorityId > 0)
                query = query.Where(t => (int)t.PriorityId == priorityId.Value);

            switch (delayType?.Trim().ToLower())
            {
                case "overdue":
                    query = query.Where(t => t.DueDate.Date < today);
                    break;
                case "soon":
                    query = query.Where(t => t.DueDate.Date >= today && t.DueDate.Date <= today.AddDays(3));
                    break;
            }

            var tasks = await query.OrderBy(t => t.DueDate).ToListAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                tasks = tasks.Where(t =>
                    t.Title.ToLower().Contains(s) ||
                    (t.Project?.Name.ToLower().Contains(s) == true)
                ).ToList();
            }

            var list = tasks.Select(t => new OverdueCriticalTaskDto
            {
                TaskId = t.Id,
                Title = t.Title,
                Description = t.Description,
                ProjectName = t.Project?.Name ?? "-",
                StatusName = StatusMap.TryGetValue(t.StatusId, out var s) ? s : $"Status {t.StatusId}",
                PriorityName = PriorityMap.TryGetValue(t.PriorityId, out var p) ? p : $"Priority {t.PriorityId}",
                AssignedTo = t.AssignedToNavigation != null ? $"{t.AssignedToNavigation.FirstName} {t.AssignedToNavigation.LastName}" : null,
                DueDate = t.DueDate,
                DaysOverdue = t.DueDate < now ? (int)(now - t.DueDate).TotalDays : 0,
                CreatedAt = t.CreatedAt ?? DateTime.UtcNow
            }).ToList();

            return Result<IEnumerable<OverdueCriticalTaskDto>>.Success(list);
        }

        public async Task<PagedResult<OverdueCriticalTaskDto>> GetPagedOverdueCriticalTasksAsync(
            string? search,
            long? projectId,
            string roleName,
            long currentUserId,
            int page,
            int pageSize,
            int? priorityId = null,
            string? delayType = null)
        {
            var now = DateTime.UtcNow;
            var today = DateTime.Today;
            var query = BuildAccessibleTaskQuery(roleName, currentUserId)
                .Include(t => t.Project)
                .Include(t => t.AssignedToNavigation)
                .Where(t => t.IsDeleted != true && t.StatusId != AppTaskStatus.Done &&
                            (t.DueDate < now || t.PriorityId == TaskPriority.High));

            if (projectId.HasValue && projectId > 0)
                query = query.Where(t => t.ProjectId == projectId.Value);

            if (priorityId.HasValue && priorityId > 0)
                query = query.Where(t => (int)t.PriorityId == priorityId.Value);

            switch (delayType?.Trim().ToLower())
            {
                case "overdue":
                    query = query.Where(t => t.DueDate.Date < today);
                    break;
                case "soon":
                    query = query.Where(t => t.DueDate.Date >= today && t.DueDate.Date <= today.AddDays(3));
                    break;
                default:
                    query = query.Where(t => t.DueDate.Date <= today.AddDays(3));
                    break;
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(t =>
                    t.Title.ToLower().Contains(s) ||
                    (t.Project != null && t.Project.Name.ToLower().Contains(s)));
            }

            var paged = await query
                .OrderBy(t => t.DueDate)
                .ThenBy(t => t.Title)
                .ToPagedResultAsync(page, pageSize);

            var mapped = MapPagedResult(paged, t => new OverdueCriticalTaskDto
            {
                TaskId = t.Id,
                Title = t.Title,
                Description = t.Description,
                ProjectName = t.Project?.Name ?? "-",
                StatusName = StatusMap.TryGetValue(t.StatusId, out var s) ? s : $"Status {t.StatusId}",
                PriorityName = PriorityMap.TryGetValue(t.PriorityId, out var p) ? p : $"Priority {t.PriorityId}",
                AssignedTo = t.AssignedToNavigation != null ? $"{t.AssignedToNavigation.FirstName} {t.AssignedToNavigation.LastName}" : null,
                DueDate = t.DueDate,
                DaysOverdue = t.DueDate < now ? (int)(now - t.DueDate).TotalDays : 0,
                CreatedAt = t.CreatedAt ?? DateTime.UtcNow
            });

            return mapped;
        }

        public byte[] ExportOverdueCriticalToExcel(IEnumerable<OverdueCriticalTaskDto> data)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Overdue & Critical Tasks");
            var headers = new[] { "Task ID", "Title", "Project", "Status", "Priority", "Assigned To", "Due Date", "Days Overdue", "Created At" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DC2626");
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
            }
            int row = 2;
            foreach (var item in data)
            {
                ws.Cell(row, 1).Value = item.TaskId;
                ws.Cell(row, 2).Value = item.Title;
                ws.Cell(row, 3).Value = item.ProjectName;
                ws.Cell(row, 4).Value = item.StatusName;
                ws.Cell(row, 5).Value = item.PriorityName;
                ws.Cell(row, 6).Value = item.AssignedTo ?? "Unassigned";
                ws.Cell(row, 7).Value = DisplayFormats.Date(item.DueDate);
                ws.Cell(row, 8).Value = item.DaysOverdue;
                ws.Cell(row, 9).Value = DisplayFormats.Date(item.CreatedAt);
                if (item.DaysOverdue > 0) ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2");
                row++;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        public async Task<Result<TimeTrackingReportDto>> GetTimeTrackingReportAsync(
            string? search, long? projectId, AppTaskStatus? statusId, string roleName, long currentUserId)
        {
            var query = BuildAccessibleTaskQuery(roleName, currentUserId)
                .Include(t => t.Project)
                .Include(t => t.AssignedToNavigation)
                .Include(t => t.TimeLogs.Where(tl => !tl.IsDeleted))
                .Where(t => t.IsDeleted != true);

            if (projectId.HasValue && projectId > 0)
                query = query.Where(t => t.ProjectId == projectId.Value);
            if (statusId.HasValue && Convert.ToInt64(statusId.Value) > 0)
                query = query.Where(t => t.StatusId == statusId.Value);

            var tasks = await query.ToListAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                tasks = tasks.Where(t =>
                    t.Title.ToLower().Contains(s) ||
                    (t.Project?.Name.ToLower().Contains(s) == true) ||
                    (t.AssignedToNavigation != null &&
                     ($"{t.AssignedToNavigation.FirstName} {t.AssignedToNavigation.LastName}").ToLower().Contains(s))
                ).ToList();
            }

            var totalEstimatedHrs = tasks.Sum(t => t.EstimatedHours ?? 0m);
            var completedHours = tasks.Sum(t => t.TimeLogs.Sum(tl => tl.HoursLogged));
            var variance = totalEstimatedHrs - completedHours;
            var completionPercentage = totalEstimatedHrs > 0m 
                ? (double)Math.Round((completedHours / totalEstimatedHrs) * 100m, 2) 
                : 0.0;

            var tasksWithHours = tasks.Count(t => t.EstimatedHours.HasValue || t.TimeLogs.Any());
            var avgHoursPerTask = tasksWithHours > 0 
                ? Math.Round(totalEstimatedHrs / tasksWithHours, 2) 
                : 0m;

            var result = new TimeTrackingReportDto
            {
                TotalEstimatedHrs = totalEstimatedHrs,
                CompletedHours = completedHours,
                Variance = variance,
                CompletionPercentage = completionPercentage,
                TasksWithHours = tasksWithHours,
                AvgHoursPerTask = avgHoursPerTask
            };

            // Group by Employee
            var employeeGroups = tasks
                .GroupBy(t => t.AssignedTo)
                .ToList();

            foreach (var group in employeeGroups)
            {
                var userId = group.Key;
                var user = userId.HasValue ? await _db.Users.FindAsync(userId.Value) : null;

                var total = group.Count();
                var completed = group.Count(t => t.StatusId == AppTaskStatus.Done);
                var est = group.Sum(t => t.EstimatedHours ?? 0m);
                var comp = group.Sum(t => t.TimeLogs.Sum(tl => tl.HoursLogged));
                var employeeVariance = est - comp;
                var employeePct = est > 0m 
                    ? (double)Math.Round((comp / est) * 100m, 2) 
                    : 0.0;

                result.EmployeeSummary.Add(new EmployeeTimeSummaryDto
                {
                    UserId = group.Key ?? 0,
                    FullName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : "Unassigned",
                    Username = user?.Username ?? "unassigned",
                    TotalTasks = total,
                    CompletedTasks = completed,
                    EstHours = est,
                    CompletedHours = comp,
                    Variance = employeeVariance,
                    CompletionPercentage = employeePct
                });
            }

            // Group by Project
            var projectGroups = tasks
                .GroupBy(t => new { t.ProjectId, ProjectName = t.Project?.Name ?? "Unknown Project" })
                .ToList();

            foreach (var group in projectGroups)
            {
                var total = group.Count();
                var completed = group.Count(t => t.StatusId == AppTaskStatus.Done);
                var est = group.Sum(t => t.EstimatedHours ?? 0m);
                var comp = group.Sum(t => t.TimeLogs.Sum(tl => tl.HoursLogged));
                var projectVariance = est - comp;
                var projectPct = est > 0m 
                    ? (double)Math.Round((comp / est) * 100m, 2) 
                    : 0.0;

                result.ProjectSummary.Add(new ProjectTimeSummaryDto
                {
                    ProjectId = group.Key.ProjectId,
                    ProjectName = group.Key.ProjectName,
                    TotalTasks = total,
                    CompletedTasks = completed,
                    EstHours = est,
                    CompletedHours = comp,
                    Variance = projectVariance,
                    CompletionPercentage = projectPct
                });
            }

            // Task Details
            foreach (var t in tasks)
            {
                result.TaskDetail.Add(new TaskTimeDetailDto
                {
                    TaskId = t.Id,
                    Title = t.Title,
                    ProjectId = t.ProjectId,
                    ProjectName = t.Project?.Name ?? "",
                    AssignedTo = t.AssignedTo,
                    AssigneeName = t.AssignedToNavigation != null ? $"{t.AssignedToNavigation.FirstName} {t.AssignedToNavigation.LastName}".Trim() : "Unassigned",
                    AssigneeUsername = t.AssignedToNavigation?.Username ?? "",
                    EstimatedHours = t.EstimatedHours,
                    CompletedHours = t.TimeLogs.Sum(tl => tl.HoursLogged),
                    DueDate = t.DueDate,
                    StatusId = t.StatusId
                });
            }

            return Result<TimeTrackingReportDto>.Success(result);
        }

        public async Task<Result<IEnumerable<EmployeeProductivityReportDto>>> GetEmployeeProductivityReportAsync(
            string? search,
            DateTime? startDate,
            DateTime? endDate,
            string? status,
            string roleName,
            long currentUserId)
        {
            var users = await BuildAccessibleUserQuery(roleName, currentUserId).ToListAsync();
            var tasks = await BuildAccessibleTaskQuery(roleName, currentUserId)
                .Include(t => t.TaskHistories)
                .ToListAsync();

            if (startDate.HasValue)
                tasks = tasks.Where(t => t.CreatedAt >= startDate.Value.Date).ToList();

            if (endDate.HasValue)
                tasks = tasks.Where(t => t.CreatedAt < endDate.Value.Date.AddDays(1)).ToList();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var sl = status.Trim().ToLower();
                if (sl == "overdue")
                {
                    tasks = tasks.Where(t => t.StatusId != AppTaskStatus.Done && t.DueDate < DateTime.Today).ToList();
                }
                else if (sl != "0")
                {
                    if (Enum.TryParse<AppTaskStatus>(status, true, out var parsedStatus))
                        tasks = tasks.Where(t => t.StatusId == parsedStatus).ToList();
                }
            }

            var list = new List<EmployeeProductivityReportDto>();
            foreach (var user in users)
            {
                var userTasks = tasks.Where(t => t.AssignedTo == user.Id).ToList();
                int total = userTasks.Count;
                var completedTasks = userTasks.Where(t => t.StatusId == AppTaskStatus.Done).ToList();
                int done = completedTasks.Count;

                int onTimeCount = completedTasks.Count(t =>
                    t.TaskHistories.Any(th => th.NewStatusId == AppTaskStatus.Done && th.CreatedAt <= t.DueDate));

                double onTimeDeliveryRate = done > 0 ? Math.Round(((double)onTimeCount / done) * 100, 2) : 0;

                list.Add(new EmployeeProductivityReportDto
                {
                    UserId = user.Id,
                    Username = user.Username,
                    FullName = $"{user.FirstName} {user.LastName}".Trim(),
                    AssignedCount = total,
                    CompletedCount = done,
                    Efficiency = total > 0 ? Math.Round(((double)done / total) * 100, 2) : 0,
                    OnTimeDeliveryRate = onTimeDeliveryRate
                });
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                list = list.Where(u => u.FullName.ToLower().Contains(s) || u.Username.ToLower().Contains(s)).ToList();
            }

            return Result<IEnumerable<EmployeeProductivityReportDto>>.Success(list.OrderByDescending(u => u.Efficiency));
        }

        public async Task<PagedResult<EmployeeProductivityReportDto>> GetPagedEmployeeProductivityReportAsync(
            string? search,
            DateTime? startDate,
            DateTime? endDate,
            string? status,
            string roleName,
            long currentUserId,
            int page,
            int pageSize)
        {
            var full = await GetEmployeeProductivityReportAsync(search, startDate, endDate, status, roleName, currentUserId);
            if (!full.IsSuccess || full.Value == null)
            {
                return new PagedResult<EmployeeProductivityReportDto>();
            }

            var ordered = full.Value.OrderByDescending(u => u.Efficiency).ToList();
            var normalizedPageSize = PaginationExtensions.NormalizePageSize(pageSize);
            var totalCount = ordered.Count;
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
            var normalizedPage = Math.Max(page, 1);
            if (totalPages > 0 && normalizedPage > totalPages)
                normalizedPage = totalPages;

            var items = ordered
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToList();

            return new PagedResult<EmployeeProductivityReportDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalPages = totalPages
            };
        }

        public async Task<Result<IEnumerable<ProjectProgressReportDto>>> GetProjectProgressReportAsync(
            string? search,
            DateTime? startDate,
            DateTime? endDate,
            string? status,
            string roleName,
            long currentUserId)
        {
            var projects = await BuildAccessibleProjectQuery(roleName, currentUserId).ToListAsync();
            var tasks = await BuildAccessibleTaskQuery(roleName, currentUserId).ToListAsync();
            var today = DateTime.Today;

            if (startDate.HasValue)
                projects = projects.Where(p => p.StartDate >= startDate.Value.Date).ToList();

            if (endDate.HasValue)
                projects = projects.Where(p => p.StartDate < endDate.Value.Date.AddDays(1)).ToList();

            var list = new List<ProjectProgressReportDto>();
            foreach (var project in projects)
            {
                var projectTasks = tasks.Where(t => t.ProjectId == project.Id).ToList();

                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (status.Equals("overdue", StringComparison.OrdinalIgnoreCase))
                    {
                        projectTasks = projectTasks.Where(t => t.DueDate.Date < today && t.StatusId != AppTaskStatus.Done).ToList();
                    }
                    else if (status != "0" && Enum.TryParse<AppTaskStatus>(status, true, out var parsedStatus))
                    {
                        projectTasks = projectTasks.Where(t => t.StatusId == parsedStatus).ToList();
                    }
                }

                int totalTasks = projectTasks.Count;
                int completedTasks = projectTasks.Count(t => t.StatusId == AppTaskStatus.Done);
                double progress = totalTasks > 0 ? ((double)completedTasks / totalTasks) * 100 : 0;

                double elapsedPct;
                if (today < project.StartDate) elapsedPct = 0;
                else if (today > project.EndDate) elapsedPct = 100;
                else
                {
                    var totalDays = (project.EndDate - project.StartDate).TotalDays;
                    var elapsedDays = (today - project.StartDate).TotalDays;
                    elapsedPct = totalDays > 0 ? (elapsedDays / totalDays) * 100 : 100;
                }

                bool isAhead = progress > elapsedPct && progress > 0 && progress < 100;
                bool isAtRisk = (projectTasks.Any(t => t.StatusId != AppTaskStatus.Done && t.DueDate.Date < today)) ||
                                (project.EndDate.Date < today && progress < 100);

                list.Add(new ProjectProgressReportDto
                {
                    ProjectId = project.Id,
                    ProjectName = project.Name,
                    StartDate = project.StartDate,
                    EndDate = project.EndDate,
                    TotalTasks = totalTasks,
                    CompletedTasks = completedTasks,
                    Progress = progress,
                    IsAhead = isAhead,
                    IsAtRisk = isAtRisk
                });
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                list = list.Where(p => p.ProjectName.ToLower().Contains(s)).ToList();
            }

            return Result<IEnumerable<ProjectProgressReportDto>>.Success(list.OrderBy(p => p.ProjectName));
        }

        public async Task<PagedResult<ProjectProgressReportDto>> GetPagedProjectProgressReportAsync(
            string? search,
            DateTime? startDate,
            DateTime? endDate,
            string? status,
            string roleName,
            long currentUserId,
            int page,
            int pageSize)
        {
            var full = await GetProjectProgressReportAsync(search, startDate, endDate, status, roleName, currentUserId);
            if (!full.IsSuccess || full.Value == null)
            {
                return new PagedResult<ProjectProgressReportDto>();
            }

            var ordered = full.Value.OrderBy(p => p.ProjectName).ToList();
            var normalizedPageSize = PaginationExtensions.NormalizePageSize(pageSize);
            var totalCount = ordered.Count;
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
            var normalizedPage = Math.Max(page, 1);
            if (totalPages > 0 && normalizedPage > totalPages)
                normalizedPage = totalPages;

            var items = ordered
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToList();

            return new PagedResult<ProjectProgressReportDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalPages = totalPages
            };
        }
    }
}

