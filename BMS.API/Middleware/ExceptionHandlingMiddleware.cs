using System.Text.Json;
using BMS.API.Models;
using BMS.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace BMS.API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {

            context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            var (status, message) = Map(ex);
            if (status >= 500) logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            else logger.LogWarning("{Method} {Path} -> {Status}: {Message}", context.Request.Method, context.Request.Path, status, message);

            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(ApiResponse.Fail(message), JsonOptions));
        }
    }

    private static (int Status, string Message) Map(Exception ex) => ex switch
    {
        AppException app => (app.StatusCode, app.Message),
        DbUpdateConcurrencyException => (409, "This record was changed by someone else. Please reload and try again."),
        DbUpdateException => (409, "The change conflicts with existing data (for example a duplicate)."),
        BadHttpRequestException => (400, "The request could not be read."),
        JsonException => (400, "The request body is not valid JSON."),
        _ => (500, "An unexpected error occurred. Please try again later.")
    };
}

