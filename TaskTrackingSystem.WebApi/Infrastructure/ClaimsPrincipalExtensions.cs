using System.Security.Claims;

namespace TaskTrackingSystem.WebApi.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static long GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var id) ? id : 0;
    }

    public static long GetRoleId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("role_id");
        return long.TryParse(value, out var id) ? id : 0;
    }

    public static string GetRoleName(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    }



}
