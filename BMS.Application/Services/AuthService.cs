using BMS.Application.Common;
using BMS.Application.Domain;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BMS.Application.Services;

public class AuthService(UserManager<AppUser> users, ITokenService tokens, IClock clock) : IAuthService
{

    public async Task<AuthUserDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null)
            throw new ConflictException("An account with this e-mail address already exists.");

        var user = new AppUser { UserName = email, Email = email, FullName = request.FullName.Trim(), IsActive = true };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            throw new BusinessRuleException(string.Join(" ", created.Errors.Select(e => e.Description)));

        await users.AddToRoleAsync(user, Roles.Viewer);
        return new AuthUserDto(user.Id, user.FullName, email, Roles.Viewer);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        const string invalid = "Invalid email or password.";
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive) throw new UnauthorizedException(invalid);

        if (await users.IsLockedOutAsync(user))
            throw new UnauthorizedException("This account is temporarily locked after too many failed attempts. Try again later.");

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            throw new UnauthorizedException(invalid);
        }

        await users.ResetAccessFailedCountAsync(user);
        return await IssueTokensAsync(user);
    }

    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var hash = tokens.HashToken(request.RefreshToken);
        var user = await users.Users.FirstOrDefaultAsync(u => u.RefreshTokenHash == hash, ct);

        if (user is null || !user.IsActive || user.RefreshTokenExpiresAt is null || user.RefreshTokenExpiresAt < clock.UtcNow)
            throw new UnauthorizedException("The refresh token is invalid or has expired. Please sign in again.");

        return await IssueTokensAsync(user);
    }

    public async Task<AuthUserDto> AssignRoleAsync(AssignRoleRequest request, CancellationToken ct = default)
    {
        var role = Roles.All.FirstOrDefault(r => r.Equals(request.Role, StringComparison.OrdinalIgnoreCase))
                   ?? throw new BusinessRuleException($"Role must be one of: {string.Join(", ", Roles.All)}.");

        var user = await users.FindByEmailAsync(request.Email.Trim())
                   ?? throw new NotFoundException("User not found.");

        var current = await users.GetRolesAsync(user);
        if (current.Contains(Roles.Admin) && role != Roles.Admin)
        {
            var admins = await users.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count <= 1) throw new BusinessRuleException("You cannot remove the last administrator.");
        }

        if (current.Count > 0) await users.RemoveFromRolesAsync(user, current);
        await users.AddToRoleAsync(user, role);
        return new AuthUserDto(user.Id, user.FullName, user.Email ?? string.Empty, role);
    }

    private async Task<LoginResponse> IssueTokensAsync(AppUser user)
    {
        var roles = await users.GetRolesAsync(user);
        var (token, expiresAt) = tokens.CreateAccessToken(user, roles);

        var refresh = tokens.GenerateRefreshToken();
        user.RefreshTokenHash = tokens.HashToken(refresh);
        user.RefreshTokenExpiresAt = clock.UtcNow.Add(tokens.RefreshTokenLifetime);
        await users.UpdateAsync(user);

        var dto = new AuthUserDto(user.Id, user.FullName, user.Email ?? string.Empty, roles.FirstOrDefault() ?? Roles.Viewer);
        return new LoginResponse(token, refresh, expiresAt, dto);
    }
}

