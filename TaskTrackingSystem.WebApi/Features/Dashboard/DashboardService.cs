using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared;
using TaskTrackingSystem.Shared.Models.Dashboard;
using TaskTrackingSystem.Shared.Enums;
using RoleEntity = TaskTrackingSystem.Database.AppDbContextModels.Role;

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

            // Status mappings (StatusId 1 = To Do, 2 = In Progress, 3 = Done etc.)
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
                int completedTasks = tasks.Count(t => t.StatusId == AppTaskStatus.Done); // StatusId 3 = Done

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

        public async Task<Result<IEnumerable<DashboardWidgetDto>>> GetWidgetsAsync(string roleName, long currentUserId)
        {
            var role = await GetRoleAsync(roleName);
            if (role == null)
            {
                return Result<IEnumerable<DashboardWidgetDto>>.Failure("Role not found.", 404);
            }

            var roleWidgets = await (
                from access in _db.RoleDashboardWidgets
                join widget in _db.DashboardWidgets on access.WidgetId equals widget.WidgetId
                where access.RoleId == role.Id &&
                      !access.IsDeleted &&
                      access.CanView &&
                      !widget.IsDeleted &&
                      widget.IsActive
                orderby access.DefaultSortOrder, widget.DefaultOrder, widget.WidgetName
                select new
                {
                    access.WidgetId,
                    access.CanView,
                    access.CanConfigure,
                    access.IsDefaultVisible,
                    RoleDefaultGridX = access.DefaultGridX,
                    RoleDefaultGridY = access.DefaultGridY,
                    RoleDefaultWidth = access.DefaultWidth,
                    RoleDefaultHeight = access.DefaultHeight,
                    RoleDefaultSortOrder = access.DefaultSortOrder,
                    widget.WidgetCode,
                    widget.WidgetName,
                    widget.Description,
                    widget.Category,
                    widget.ComponentKey,
                    widget.DataSourceKey,
                    WidgetDefaultWidth = widget.DefaultWidth,
                    WidgetDefaultHeight = widget.DefaultHeight,
                    WidgetDefaultOrder = widget.DefaultOrder
                }).ToListAsync();

            var userLayouts = await _db.UserDashboardLayouts
                .Where(layout => layout.UserId == currentUserId && !layout.IsDeleted)
                .ToListAsync();

            var layoutLookup = userLayouts.ToDictionary(layout => layout.WidgetId);

            var widgets = roleWidgets
                .Select(widget =>
                {
                    layoutLookup.TryGetValue(widget.WidgetId, out var layout);

                    var width = layout != null ? layout.Width : (widget.RoleDefaultWidth > 0 ? widget.RoleDefaultWidth : widget.WidgetDefaultWidth);
                    var height = layout != null ? layout.Height : (widget.RoleDefaultHeight > 0 ? widget.RoleDefaultHeight : widget.WidgetDefaultHeight);
                    var sortOrder = layout != null ? layout.SortOrder : widget.RoleDefaultSortOrder;

                    return new DashboardWidgetDto
                    {
                        WidgetId = widget.WidgetId,
                        WidgetCode = widget.WidgetCode,
                        WidgetName = widget.WidgetName,
                        Description = widget.Description,
                        Category = widget.Category,
                        ComponentKey = widget.ComponentKey,
                        DataSourceKey = widget.DataSourceKey,
                        DefaultWidth = widget.WidgetDefaultWidth,
                        DefaultHeight = widget.WidgetDefaultHeight,
                        DefaultOrder = widget.WidgetDefaultOrder,
                        CanView = widget.CanView,
                        CanConfigure = widget.CanConfigure,
                        IsDefaultVisible = widget.IsDefaultVisible,
                        HasCustomLayout = layout != null,
                        IsHidden = layout?.IsHidden ?? !widget.IsDefaultVisible,
                        IsPinned = layout?.IsPinned ?? false,
                        GridX = layout?.GridX ?? widget.RoleDefaultGridX,
                        GridY = layout?.GridY ?? widget.RoleDefaultGridY,
                        Width = width,
                        Height = height,
                        SortOrder = sortOrder,
                        ConfigJson = layout?.ConfigJson
                    };
                })
                .OrderBy(widget => widget.SortOrder)
                .ThenBy(widget => widget.GridY)
                .ThenBy(widget => widget.GridX)
                .ThenBy(widget => widget.DefaultOrder)
                .ToList();

            return Result<IEnumerable<DashboardWidgetDto>>.Success(widgets);
        }

        public async Task<Result> SaveWidgetLayoutAsync(string roleName, long currentUserId, DashboardWidgetLayoutSaveRequestDto request)
        {
            var role = await GetRoleAsync(roleName);
            if (role == null)
            {
                return Result.Failure("Role not found.", 404);
            }

            if (request == null)
            {
                return Result.Failure("Widget layout is required.", 400);
            }

            if (request.Widgets == null)
            {
                return Result.Failure("Widget layout is required.", 400);
            }

            var allowedWidgetIds = await _db.RoleDashboardWidgets
                .Where(access => access.RoleId == role.Id && !access.IsDeleted && access.CanView)
                .Select(access => access.WidgetId)
                .ToHashSetAsync();

            var requestedWidgets = request.Widgets
                .GroupBy(widget => widget.WidgetId)
                .Select(group => group.Last())
                .ToList();

            var invalidWidgets = requestedWidgets
                .Where(widget => !allowedWidgetIds.Contains(widget.WidgetId))
                .Select(widget => widget.WidgetId)
                .Distinct()
                .ToList();

            if (invalidWidgets.Any())
            {
                return Result.Failure($"The following widgets are not available for this role: {string.Join(", ", invalidWidgets)}", 403);
            }

            var existingLayouts = await _db.UserDashboardLayouts
                .Where(layout => layout.UserId == currentUserId && !layout.IsDeleted)
                .ToListAsync();
            var existingLookup = existingLayouts.ToDictionary(layout => layout.WidgetId);

            var now = DateTime.UtcNow;
            foreach (var widget in requestedWidgets)
            {
                if (existingLookup.TryGetValue(widget.WidgetId, out var existing))
                {
                    existing.GridX = widget.GridX;
                    existing.GridY = widget.GridY;
                    existing.Width = widget.Width > 0 ? widget.Width : existing.Width;
                    existing.Height = widget.Height > 0 ? widget.Height : existing.Height;
                    existing.SortOrder = widget.SortOrder;
                    existing.IsHidden = widget.IsHidden;
                    existing.IsPinned = widget.IsPinned;
                    existing.ConfigJson = widget.ConfigJson;
                    existing.IsDeleted = false;
                    existing.UpdatedAt = now;
                    existing.UpdatedById = currentUserId;
                }
                else
                {
                    _db.UserDashboardLayouts.Add(new UserDashboardLayout
                    {
                        UserId = currentUserId,
                        WidgetId = widget.WidgetId,
                        GridX = widget.GridX,
                        GridY = widget.GridY,
                        Width = widget.Width > 0 ? widget.Width : 4,
                        Height = widget.Height > 0 ? widget.Height : 3,
                        SortOrder = widget.SortOrder,
                        IsHidden = widget.IsHidden,
                        IsPinned = widget.IsPinned,
                        ConfigJson = widget.ConfigJson,
                        IsDeleted = false,
                        CreatedAt = now,
                        UpdatedAt = now,
                        CreatedById = currentUserId,
                        UpdatedById = currentUserId
                    });
                }
            }

            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        public async Task<Result<IEnumerable<DashboardWidgetAdminDto>>> GetWidgetCatalogAsync()
        {
            var roleCounts = await _db.RoleDashboardWidgets
                .Where(access => !access.IsDeleted)
                .GroupBy(access => access.WidgetId)
                .Select(group => new
                {
                    WidgetId = group.Key,
                    RoleCount = group.Select(item => item.RoleId).Distinct().Count()
                })
                .ToListAsync();

            var roleCountLookup = roleCounts.ToDictionary(x => x.WidgetId, x => x.RoleCount);

            var widgets = await _db.DashboardWidgets
                .OrderBy(widget => widget.DefaultOrder)
                .ThenBy(widget => widget.WidgetName)
                .Select(widget => new DashboardWidgetAdminDto
                {
                    WidgetId = widget.WidgetId,
                    WidgetCode = widget.WidgetCode,
                    WidgetName = widget.WidgetName,
                    Description = widget.Description,
                    Category = widget.Category,
                    ComponentKey = widget.ComponentKey,
                    DataSourceKey = widget.DataSourceKey,
                    DefaultWidth = widget.DefaultWidth,
                    DefaultHeight = widget.DefaultHeight,
                    DefaultOrder = widget.DefaultOrder,
                    IsActive = widget.IsActive,
                    IsDeleted = widget.IsDeleted,
                    CreatedAt = widget.CreatedAt,
                    UpdatedAt = widget.UpdatedAt,
                    RoleCount = 0
                })
                .ToListAsync();

            foreach (var widget in widgets)
            {
                if (roleCountLookup.TryGetValue(widget.WidgetId, out var roleCount))
                {
                    widget.RoleCount = roleCount;
                }
            }

            return Result<IEnumerable<DashboardWidgetAdminDto>>.Success(widgets);
        }

        public async Task<Result<DashboardWidgetAdminDto>> GetWidgetByIdAsync(long widgetId)
        {
            var widget = await _db.DashboardWidgets.FirstOrDefaultAsync(w => w.WidgetId == widgetId);
            if (widget == null)
            {
                return Result<DashboardWidgetAdminDto>.Failure("Widget not found.", 404);
            }

            var roleCount = await _db.RoleDashboardWidgets
                .Where(access => access.WidgetId == widgetId && !access.IsDeleted)
                .Select(access => access.RoleId)
                .Distinct()
                .CountAsync();

            return Result<DashboardWidgetAdminDto>.Success(new DashboardWidgetAdminDto
            {
                WidgetId = widget.WidgetId,
                WidgetCode = widget.WidgetCode,
                WidgetName = widget.WidgetName,
                Description = widget.Description,
                Category = widget.Category,
                ComponentKey = widget.ComponentKey,
                DataSourceKey = widget.DataSourceKey,
                DefaultWidth = widget.DefaultWidth,
                DefaultHeight = widget.DefaultHeight,
                DefaultOrder = widget.DefaultOrder,
                IsActive = widget.IsActive,
                IsDeleted = widget.IsDeleted,
                CreatedAt = widget.CreatedAt,
                UpdatedAt = widget.UpdatedAt,
                RoleCount = roleCount
            });
        }

        public async Task<Result<DashboardWidgetAdminDto>> SaveWidgetAsync(DashboardWidgetUpsertDto dto, long? currentUserId = null)
        {
            if (dto == null)
            {
                return Result<DashboardWidgetAdminDto>.Failure("Widget data is required.", 400);
            }

            if (string.IsNullOrWhiteSpace(dto.WidgetCode))
            {
                return Result<DashboardWidgetAdminDto>.Failure("Widget code is required.", 400);
            }

            if (string.IsNullOrWhiteSpace(dto.WidgetName))
            {
                return Result<DashboardWidgetAdminDto>.Failure("Widget name is required.", 400);
            }

            var widgetCode = dto.WidgetCode.Trim();
            var widgetName = dto.WidgetName.Trim();

            var existingByCode = await _db.DashboardWidgets
                .FirstOrDefaultAsync(widget => widget.WidgetCode == widgetCode && widget.WidgetId != dto.WidgetId);
            if (existingByCode != null)
            {
                return Result<DashboardWidgetAdminDto>.Failure("Widget code is already in use by another widget.", 400);
            }

            var now = DateTime.UtcNow;
            DashboardWidget? widget = null;

            if (dto.WidgetId > 0)
            {
                widget = await _db.DashboardWidgets.FirstOrDefaultAsync(item => item.WidgetId == dto.WidgetId);
                if (widget == null)
                {
                    return Result<DashboardWidgetAdminDto>.Failure("Widget not found.", 404);
                }
            }
            else
            {
                widget = new DashboardWidget
                {
                    CreatedAt = now
                };
                _db.DashboardWidgets.Add(widget);
            }

            var activeWidget = widget!;

            activeWidget.WidgetCode = widgetCode;
            activeWidget.WidgetName = widgetName;
            activeWidget.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            activeWidget.Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim();
            activeWidget.ComponentKey = string.IsNullOrWhiteSpace(dto.ComponentKey) ? null : dto.ComponentKey.Trim();
            activeWidget.DataSourceKey = string.IsNullOrWhiteSpace(dto.DataSourceKey) ? null : dto.DataSourceKey.Trim();
            activeWidget.DefaultWidth = dto.DefaultWidth > 0 ? dto.DefaultWidth : 4;
            activeWidget.DefaultHeight = dto.DefaultHeight > 0 ? dto.DefaultHeight : 3;
            activeWidget.DefaultOrder = dto.DefaultOrder;
            activeWidget.IsActive = dto.IsActive;
            activeWidget.IsDeleted = false;
            activeWidget.UpdatedAt = now;

            if (dto.WidgetId > 0)
            {
                _db.DashboardWidgets.Update(activeWidget);
            }

            await _db.SaveChangesAsync();

            var roleCount = await _db.RoleDashboardWidgets
                .Where(access => access.WidgetId == activeWidget.WidgetId && !access.IsDeleted)
                .Select(access => access.RoleId)
                .Distinct()
                .CountAsync();

            return Result<DashboardWidgetAdminDto>.Success(new DashboardWidgetAdminDto
            {
                WidgetId = activeWidget.WidgetId,
                WidgetCode = activeWidget.WidgetCode,
                WidgetName = activeWidget.WidgetName,
                Description = activeWidget.Description,
                Category = activeWidget.Category,
                ComponentKey = activeWidget.ComponentKey,
                DataSourceKey = activeWidget.DataSourceKey,
                DefaultWidth = activeWidget.DefaultWidth,
                DefaultHeight = activeWidget.DefaultHeight,
                DefaultOrder = activeWidget.DefaultOrder,
                IsActive = activeWidget.IsActive,
                IsDeleted = activeWidget.IsDeleted,
                CreatedAt = activeWidget.CreatedAt,
                UpdatedAt = activeWidget.UpdatedAt,
                RoleCount = roleCount
            }, dto.WidgetId > 0 ? 200 : 201);
        }

        public async Task<Result> DeleteWidgetAsync(long widgetId, long? currentUserId = null)
        {
            var widget = await _db.DashboardWidgets.FirstOrDefaultAsync(item => item.WidgetId == widgetId);
            if (widget == null)
            {
                return Result.Failure("Widget not found.", 404);
            }

            var now = DateTime.UtcNow;
            widget.IsDeleted = true;
            widget.IsActive = false;
            widget.UpdatedAt = now;

            var roleAccessRows = await _db.RoleDashboardWidgets
                .Where(access => access.WidgetId == widgetId && !access.IsDeleted)
                .ToListAsync();
            foreach (var row in roleAccessRows)
            {
                row.IsDeleted = true;
                row.UpdatedAt = now;
                row.UpdatedById = currentUserId;
            }

            var userLayoutRows = await _db.UserDashboardLayouts
                .Where(layout => layout.WidgetId == widgetId && !layout.IsDeleted)
                .ToListAsync();
            foreach (var row in userLayoutRows)
            {
                row.IsDeleted = true;
                row.UpdatedAt = now;
                row.UpdatedById = currentUserId;
            }

            _db.DashboardWidgets.Update(widget);
            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        public async Task<Result<IEnumerable<DashboardWidgetRoleAccessDto>>> GetWidgetAccessAsync(long widgetId)
        {
            var widgetExists = await _db.DashboardWidgets.AnyAsync(widget => widget.WidgetId == widgetId && !widget.IsDeleted);
            if (!widgetExists)
            {
                return Result<IEnumerable<DashboardWidgetRoleAccessDto>>.Failure("Widget not found.", 404);
            }

            var roles = await _db.Roles
                .Where(role => !role.IsDeleted)
                .OrderBy(role => role.Name)
                .ToListAsync();

            var accessRows = await _db.RoleDashboardWidgets
                .Where(access => access.WidgetId == widgetId && !access.IsDeleted)
                .ToListAsync();
            var lookup = accessRows.ToDictionary(row => row.RoleId);

            var access = roles.Select(role =>
            {
                lookup.TryGetValue(role.Id, out var row);
                return new DashboardWidgetRoleAccessDto
                {
                    RoleId = role.Id,
                    RoleName = role.Name,
                    CanView = row?.CanView ?? false,
                    CanConfigure = row?.CanConfigure ?? false,
                    IsDefaultVisible = row?.IsDefaultVisible ?? false,
                    DefaultGridX = row?.DefaultGridX ?? 0,
                    DefaultGridY = row?.DefaultGridY ?? 0,
                    DefaultWidth = row?.DefaultWidth ?? 4,
                    DefaultHeight = row?.DefaultHeight ?? 3,
                    DefaultSortOrder = row?.DefaultSortOrder ?? 0
                };
            }).ToList();

            return Result<IEnumerable<DashboardWidgetRoleAccessDto>>.Success(access);
        }

        public async Task<Result> SaveWidgetAccessAsync(long widgetId, DashboardWidgetRoleAccessSaveRequestDto request, long? currentUserId = null)
        {
            var widget = await _db.DashboardWidgets.FirstOrDefaultAsync(item => item.WidgetId == widgetId && !item.IsDeleted);
            if (widget == null)
            {
                return Result.Failure("Widget not found.", 404);
            }

            if (request?.RoleAccess == null)
            {
                return Result.Failure("Role access data is required.", 400);
            }

            var roles = await _db.Roles
                .Where(role => !role.IsDeleted)
                .Select(role => role.Id)
                .ToHashSetAsync();

            var roleAccess = request.RoleAccess
                .Where(item => roles.Contains(item.RoleId))
                .GroupBy(item => item.RoleId)
                .Select(group => group.Last())
                .ToList();

            if (roleAccess.Count == 0)
            {
                return Result.Failure("No valid role access rows were supplied.", 400);
            }

            var existingRows = await _db.RoleDashboardWidgets
                .Where(access => access.WidgetId == widgetId)
                .ToListAsync();
            var lookup = existingRows.ToDictionary(row => row.RoleId);
            var now = DateTime.UtcNow;

            foreach (var item in roleAccess)
            {
                if (lookup.TryGetValue(item.RoleId, out var existing))
                {
                    existing.CanView = item.CanView;
                    existing.CanConfigure = item.CanConfigure;
                    existing.IsDefaultVisible = item.IsDefaultVisible;
                    existing.DefaultGridX = item.DefaultGridX;
                    existing.DefaultGridY = item.DefaultGridY;
                    existing.DefaultWidth = item.DefaultWidth > 0 ? item.DefaultWidth : 4;
                    existing.DefaultHeight = item.DefaultHeight > 0 ? item.DefaultHeight : 3;
                    existing.DefaultSortOrder = item.DefaultSortOrder;
                    existing.IsDeleted = false;
                    existing.UpdatedAt = now;
                    existing.UpdatedById = currentUserId;
                }
                else
                {
                    _db.RoleDashboardWidgets.Add(new RoleDashboardWidget
                    {
                        RoleId = item.RoleId,
                        WidgetId = widgetId,
                        CanView = item.CanView,
                        CanConfigure = item.CanConfigure,
                        IsDefaultVisible = item.IsDefaultVisible,
                        DefaultGridX = item.DefaultGridX,
                        DefaultGridY = item.DefaultGridY,
                        DefaultWidth = item.DefaultWidth > 0 ? item.DefaultWidth : 4,
                        DefaultHeight = item.DefaultHeight > 0 ? item.DefaultHeight : 3,
                        DefaultSortOrder = item.DefaultSortOrder,
                        IsDeleted = false,
                        CreatedAt = now,
                        UpdatedAt = now,
                        CreatedById = currentUserId,
                        UpdatedById = currentUserId
                    });
                }
            }

            await _db.SaveChangesAsync();
            return Result.Success(200);
        }

        private IQueryable<TaskTrackingSystem.Database.AppDbContextModels.Task> BuildAccessibleTaskQuery(string roleName, long currentUserId)
        {
            var query = _db.Tasks.Where(t => t.IsDeleted != true);

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

        private async Task<RoleEntity?> GetRoleAsync(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return null;
            }

            return await _db.Roles.FirstOrDefaultAsync(role => role.Name == roleName && !role.IsDeleted);
        }

        private static bool IsManager(string roleName)
        {
            return string.Equals(roleName, "Manager", StringComparison.OrdinalIgnoreCase);
        }
    }
}
