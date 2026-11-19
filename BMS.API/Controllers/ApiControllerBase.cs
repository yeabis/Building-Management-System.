using BMS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BMS.API.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<ApiResponse<T>> Success<T>(T data, string? message = null) =>
        base.Ok(ApiResponse.Ok(data, message));

    protected ActionResult<ApiResponse<object?>> Success(string? message = null) =>
        base.Ok(ApiResponse.Ok<object?>(null, message));
}

