using System.Security.Claims;
using BMS.Application.Interfaces;

namespace BMS.API.Services;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirstValue("sub");

    public string UserName => Principal?.FindFirstValue("email") ?? "system";
}

