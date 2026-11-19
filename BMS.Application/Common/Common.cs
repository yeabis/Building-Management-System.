using Microsoft.EntityFrameworkCore;

namespace BMS.Application.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Viewer = "Viewer";

    public const string WriteAccess = "Admin,Manager";
    public static readonly string[] All = [Admin, Manager, Viewer];
}

public abstract class AppException : Exception
{
    protected AppException(string message, int statusCode) : base(message) => StatusCode = statusCode;
    public int StatusCode { get; }
}

public class NotFoundException : AppException { public NotFoundException(string message) : base(message, 404) { } }
public class ConflictException : AppException { public ConflictException(string message) : base(message, 409) { } }
public class BusinessRuleException : AppException { public BusinessRuleException(string message) : base(message, 400) { } }
public class UnauthorizedException : AppException { public UnauthorizedException(string message) : base(message, 401) { } }

public class PageQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) =>
        new(Items.Select(map).ToList(), TotalCount, Page, PageSize);
}

public static class QueryableExtensions
{
    public const int MaxPageSize = 200;

    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T>(items, total, page, pageSize);
    }
}

