using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BMS.Application.Domain;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using ClosedXML.Excel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BMS.Infrastructure.Services;

public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "BMS.API";
    public string Audience { get; set; } = "BMS.Client";
    public int ExpiryMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 7;
}

public class JwtTokenService(IOptions<JwtSettings> options, IClock clock) : ITokenService
{
    private readonly JwtSettings _s = options.Value;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_s.RefreshTokenDays);

    public (string Token, DateTime ExpiresAt) CreateAccessToken(AppUser user, IEnumerable<string> roles)
    {
        var expires = clock.UtcNow.AddMinutes(_s.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", user.FullName)
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_s.Key));
        var jwt = new JwtSecurityToken(
            issuer: _s.Issuer,
            audience: _s.Audience,
            claims: claims,
            notBefore: clock.UtcNow,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    public string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public class SystemClock : IClock
{
    private readonly TimeZoneInfo _zone;

    public SystemClock(IConfiguration config)
    {
        var id = config["Building:TimeZone"] ?? "Africa/Addis_Ababa";
        try { _zone = TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { _zone = TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { _zone = TimeZoneInfo.Utc; }
    }

    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone));
}

public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IConfiguration config)
    {
        var configured = config["Storage:RootPath"] ?? "uploads";
        _root = Path.GetFullPath(configured, Directory.GetCurrentDirectory());
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string extension, string folder, CancellationToken ct = default)
    {
        var relative = Path.Combine(folder, $"{Guid.NewGuid():N}{extension}").Replace('\\', '/');
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var file = File.Create(full);
        await content.CopyToAsync(file, ct);
        return relative;
    }

    public bool Exists(string relativePath) => File.Exists(Resolve(relativePath));

    public Stream OpenRead(string relativePath) => File.OpenRead(Resolve(relativePath));

    private string Resolve(string relativePath)
    {
        var full = Path.GetFullPath(relativePath, _root);
        var rootWithSeparator = _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid file path.");
        return full;
    }
}

public class ExcelExporter : IExcelExporter
{
    public byte[] Export(string sheetName, IReadOnlyList<object> rows, IReadOnlyList<SummaryItem> summary)
    {
        using var wb = new XLWorkbook();

        var ws = wb.Worksheets.Add(sheetName);
        var props = rows.Count > 0
            ? rows[0].GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            : Array.Empty<PropertyInfo>();

        for (var c = 0; c < props.Length; c++)
        {
            var header = ws.Cell(1, c + 1);
            header.Value = Humanize(props[c].Name);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
        }
        for (var r = 0; r < rows.Count; r++)
            for (var c = 0; c < props.Length; c++)
                SetCell(ws.Cell(r + 2, c + 1), props[c].GetValue(rows[r]));
        if (rows.Count == 0) ws.Cell(1, 1).Value = "No data for the selected filters.";
        ws.Columns().AdjustToContents();

        var sum = wb.Worksheets.Add("Summary");
        sum.Cell(1, 1).Value = "Metric";
        sum.Cell(1, 2).Value = "Value";
        sum.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < summary.Count; i++)
        {
            sum.Cell(i + 2, 1).Value = summary[i].Label;
            SetCell(sum.Cell(i + 2, 2), summary[i].Value);
        }
        sum.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: break;
            case decimal d: cell.Value = d; cell.Style.NumberFormat.Format = "#,##0.00"; break;
            case int i: cell.Value = i; break;
            case long l: cell.Value = l; break;
            case double db: cell.Value = db; break;
            case bool bo: cell.Value = bo; break;
            case DateOnly d: cell.Value = d.ToDateTime(TimeOnly.MinValue); cell.Style.DateFormat.Format = "yyyy-mm-dd"; break;
            case DateTime dt: cell.Value = dt; break;
            default: cell.Value = value.ToString() ?? string.Empty; break;
        }
    }

    private static string Humanize(string name)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) sb.Append(' ');
            sb.Append(name[i]);
        }
        return sb.ToString();
    }
}

