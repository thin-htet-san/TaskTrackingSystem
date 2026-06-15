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

namespace TaskTrackingSystem.WebApi.Features.Report
{
    public class ReportService
    {
        private readonly AppDbContext _db;
        private static readonly Dictionary<long, string> StatusMap = new() { { 1, "To Do" }, { 2, "In Progress" }, { 3, "Done" } };
        private static readonly Dictionary<long, string> PriorityMap = new() { { 1, "Low" }, { 2, "Medium" }, { 3, "High" } };

        public ReportService(AppDbContext db)
        {
            _db = db;
        }

        // ─── Legacy endpoints (kept for backward compatibility) ───────────────────

        public async Task<Result<IEnumerable<TaskReportDto>>> GetTasksReportAsync(
            DateTime? startDate, DateTime? endDate, string? status, int? projectId)
        {
            var query = _db.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedToNavigation)
                .Include(t => t.AssignedByNavigation)
                .Where(t => t.IsDeleted != true);

            if (startDate.HasValue) query = query.Where(t => t.CreatedAt >= startDate.Value);
            if (endDate.HasValue) query = query.Where(t => t.CreatedAt <= endDate.Value);
            if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);
            if (!string.IsNullOrWhiteSpace(status))
            {
                var sl = status.Trim().ToLower();
                long? sid = sl switch { "to do" => 1, "in progress" => 2, "done" => 3, _ => null };
                if (sid.HasValue) query = query.Where(t => t.StatusId == sid.Value);
                else if (long.TryParse(status, out var pid)) query = query.Where(t => t.StatusId == pid);
            }

            var tasks = await query.ToListAsync();
            var list = tasks.Select(t => new TaskReportDto
            {
                TaskId = t.Id,
                Title = t.Title,
                Description = t.Description,
                ProjectId = t.ProjectId,
                ProjectName = t.Project.Name,
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
            }).ToList();
            return Result<IEnumerable<TaskReportDto>>.Success(list);
        }

        public async Task<Result<IEnumerable<UserProductivityDto>>> GetUserProductivityReportAsync()
        {
            var users = await _db.Users.Where(u => !u.IsDeleted).ToListAsync();
            var list = new List<UserProductivityDto>();
            foreach (var user in users)
            {
                var tasks = await _db.Tasks.Where(t => t.AssignedTo == user.Id && t.IsDeleted != true).ToListAsync();
                int total = tasks.Count, done = tasks.Count(t => t.StatusId == 3);
                list.Add(new UserProductivityDto
                {
                    UserId = user.Id,
                    Username = user.Username,
                    FullName = $"{user.FirstName} {user.LastName}".Trim(),
                    TotalAssignedTasks = total,
                    CompletedTasksCount = done,
                    EfficiencyRatio = total > 0 ? Math.Round(((double)done / total) * 100, 2) : 0
                });
            }
            return Result<IEnumerable<UserProductivityDto>>.Success(list);
        }

        // ─── Report 1: Task Status Summary ────────────────────────────────────────

        public async Task<Result<IEnumerable<TaskStatusSummaryDto>>> GetTaskStatusSummaryAsync(
            string? search, long? statusId, long? projectId)
        {
            var query = _db.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedToNavigation)
                .Where(t => t.IsDeleted != true);

            if (statusId.HasValue && statusId > 0)
                query = query.Where(t => t.StatusId == statusId.Value);
            if (projectId.HasValue && projectId > 0)
                query = query.Where(t => t.ProjectId == projectId.Value);

            var tasks = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

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
                ws.Cell(row, 7).Value = item.DueDate.ToString("yyyy-MM-dd");
                ws.Cell(row, 8).Value = item.CreatedAt.ToString("yyyy-MM-dd");
                ws.Cell(row, 9).Value = item.IsOverdue ? "Yes" : "No";
                if (item.IsOverdue) ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2");
                row++;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ─── Report 2: Team Productivity ──────────────────────────────────────────

        public async Task<Result<IEnumerable<TeamProductivityReportDto>>> GetTeamProductivityAsync(string? search)
        {
            var users = await _db.Users.Where(u => !u.IsDeleted).ToListAsync();
            var allTasks = await _db.Tasks.Where(t => t.IsDeleted != true && t.AssignedTo != null).ToListAsync();
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
                    Completed = uTasks.Count(t => t.StatusId == 3),
                    InProgress = uTasks.Count(t => t.StatusId == 2),
                    ToDo = uTasks.Count(t => t.StatusId == 1),
                    Overdue = uTasks.Count(t => t.DueDate < now && t.StatusId != 3),
                    CompletionRate = uTasks.Count > 0 ? Math.Round((double)uTasks.Count(t => t.StatusId == 3) / uTasks.Count * 100, 1) : 0
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

        // ─── Report 3: Overdue & Critical Tasks ───────────────────────────────────

        public async Task<Result<IEnumerable<OverdueCriticalTaskDto>>> GetOverdueCriticalTasksAsync(string? search, long? projectId)
        {
            var now = DateTime.UtcNow;
            var query = _db.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedToNavigation)
                .Where(t => t.IsDeleted != true && t.StatusId != 3 &&
                            (t.DueDate < now || t.PriorityId == 3)); // overdue OR high priority

            if (projectId.HasValue && projectId > 0)
                query = query.Where(t => t.ProjectId == projectId.Value);

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
                ws.Cell(row, 7).Value = item.DueDate.ToString("yyyy-MM-dd");
                ws.Cell(row, 8).Value = item.DaysOverdue;
                ws.Cell(row, 9).Value = item.CreatedAt.ToString("yyyy-MM-dd");
                if (item.DaysOverdue > 0) ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2");
                row++;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }
}
