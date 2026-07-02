using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaskTrackingSystem.Database.AppDbContextModels;

namespace TaskTrackingSystem.WebApi.Infrastructure;

public class PermissionAuthorizationService
{
    private readonly AppDbContext _db;

    public PermissionAuthorizationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> CanAccessAsync(ClaimsPrincipal user, string apiName, string actionName)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var roleId = await ResolveRoleIdAsync(user);
        if (roleId <= 0)
        {
            return false;
        }

        return await _db.RolePermissions
            .AnyAsync(rp =>
                rp.RoleId == roleId &&
                !rp.IsDeleted &&
                !rp.Permission.IsDeleted &&
                rp.Permission.ApiName == apiName &&
                rp.Permission.ActionName == actionName);
    }

    private async Task<long> ResolveRoleIdAsync(ClaimsPrincipal user)
    {
        var roleId = user.GetRoleId();
        if (roleId > 0)
        {
            return roleId;
        }

        var roleName = user.GetRoleName();
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return 0;
        }

        return await _db.Roles
            .Where(r => r.Name == roleName && r.IsDeleted != true)
            .Select(r => r.Id)
            .FirstOrDefaultAsync();
    }
}
