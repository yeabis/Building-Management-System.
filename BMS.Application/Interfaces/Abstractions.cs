using BMS.Application.Domain;
using BMS.Application.DTOs;

namespace BMS.Application.Interfaces;

public interface IRepository<T> where T : BaseEntity
{

    IQueryable<T> Query();

    IQueryable<T> QueryTracked();
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
    void Update(T entity);

    void Remove(T entity);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface ICurrentUser
{
    string? UserId { get; }

    string UserName { get; }
}

public interface IClock
{
    DateTime UtcNow { get; }

    DateOnly Today { get; }
}

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string extension, string folder, CancellationToken ct = default);
    bool Exists(string relativePath);
    Stream OpenRead(string relativePath);
}

public interface ITokenService
{
    TimeSpan RefreshTokenLifetime { get; }
    (string Token, DateTime ExpiresAt) CreateAccessToken(AppUser user, IEnumerable<string> roles);
    string GenerateRefreshToken();
    string HashToken(string token);
}

public interface IExcelExporter
{
    byte[] Export(string sheetName, IReadOnlyList<object> rows, IReadOnlyList<SummaryItem> summary);
}

