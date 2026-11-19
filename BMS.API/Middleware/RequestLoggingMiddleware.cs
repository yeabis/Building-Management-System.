using System.Diagnostics;

namespace BMS.API.Middleware;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            var user = context.User.Identity?.IsAuthenticated == true ? context.User.FindFirst("email")?.Value : "anonymous";
            logger.LogInformation("HTTP {Method} {Path} -> {Status} in {Elapsed} ms (user: {User})",
                context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, sw.ElapsedMilliseconds, user);
        }
    }
}

