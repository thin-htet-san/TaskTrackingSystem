using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using TaskTrackingSystem.Database.AppDbContextModels;

namespace TaskTrackingSystem.WebApi.Infrastructure
{
    public static class DataScopeAuthorization
    {
        public static async Task<bool> IsAdminScopeAsync(AppDbContext db, long roleId)
        {
            if (roleId <= 0) return false;
            return await db.RolePermissions.AnyAsync(rp => 
                rp.RoleId == roleId && 
                !rp.IsDeleted && 
                rp.Permission.PermissionCode == "Roles_List" && 
                !rp.Permission.IsDeleted);
        }

        public static async Task<bool> IsManagerScopeAsync(AppDbContext db, long roleId)
        {
            if (roleId <= 0) return false;
            return await db.RolePermissions.AnyAsync(rp => 
                rp.RoleId == roleId && 
                !rp.IsDeleted && 
                rp.Permission.PermissionCode == "Projects_Create" && 
                !rp.Permission.IsDeleted);
        }
    }
}
