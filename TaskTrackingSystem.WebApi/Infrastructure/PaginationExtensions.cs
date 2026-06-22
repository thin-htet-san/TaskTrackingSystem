using Microsoft.EntityFrameworkCore;
using TaskTrackingSystem.Shared;

namespace TaskTrackingSystem.WebApi.Infrastructure;

public static class PaginationExtensions
{
    public static int NormalizePage(int? page)
    {
        return page.HasValue && page.Value > 0 ? page.Value : 1;
    }

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        pageSize = NormalizePageSize(pageSize);
        page = Math.Max(page, 1);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        if (totalPages > 0 && page > totalPages)
        {
            page = totalPages;
        }

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public static int NormalizePageSize(int pageSize)
    {
        if (pageSize <= 0)
        {
            return 20;
        }

        return Math.Clamp(pageSize, 1, 100);
    }
}
