using System.Text.Json.Serialization;

namespace BMS.API.Models;

public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; init; }
}

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<object?> Fail(string message, IDictionary<string, string[]>? errors = null) =>
        new() { Success = false, Data = null, Message = message, Errors = errors };
}

public class DocumentUploadForm
{
    public IFormFile File { get; set; } = null!;
    public BMS.Application.Domain.DocumentType DocumentType { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}

