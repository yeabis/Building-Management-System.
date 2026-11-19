using BMS.API.Models;
using BMS.Application.Common;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BMS.API.Controllers;

[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController(IAuthService auth) : ApiControllerBase
{

    [HttpPost("login"), AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request, CancellationToken ct) =>
        Success(await auth.LoginAsync(request, ct));

    [HttpPost("register"), AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthUserDto>>> Register([FromBody] RegisterRequest request, CancellationToken ct) =>
        Success(await auth.RegisterAsync(request, ct), "Account created.");

    [HttpPost("refresh"), AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct) =>
        Success(await auth.RefreshTokenAsync(request, ct));

    [HttpPost("assign-role"), Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<AuthUserDto>>> AssignRole([FromBody] AssignRoleRequest request, CancellationToken ct) =>
        Success(await auth.AssignRoleAsync(request, ct), "Role updated.");
}

