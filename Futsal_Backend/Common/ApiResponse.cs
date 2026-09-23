using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Backend.Common;

public class ApiError
{
    public string Code { get; set; } = "error";
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string[]>? Details { get; set; }
}

/// <summary>Envelope used by every endpoint: { success, data, error }.</summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public ApiError? Error { get; set; }

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
}

public static class ApiResponse
{
    public static ApiResponse<object> Fail(string code, string message, Dictionary<string, string[]>? details = null) =>
        new()
        {
            Success = false,
            Error = new ApiError { Code = code, Message = message, Details = details },
        };

    /// <summary>Turns MVC model validation errors into the standard envelope.</summary>
    public static ApiResponse<object> FromModelState(ModelStateDictionary modelState)
    {
        var details = modelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .GroupBy(entry => ToCamelKey(entry.Key))
            .ToDictionary(
                group => group.Key,
                group => group
                    .SelectMany(entry => entry.Value!.Errors)
                    .Select(err => string.IsNullOrWhiteSpace(err.ErrorMessage) ? "Invalid value." : err.ErrorMessage)
                    .Distinct()
                    .ToArray());

        var first = details.Values.SelectMany(v => v).FirstOrDefault() ?? "The request is invalid.";
        return Fail("validation_error", first, details);
    }

    private static string ToCamelKey(string key)
    {
        var trimmed = key.TrimStart('$', '.');
        return trimmed.Length == 0 ? trimmed : char.ToLowerInvariant(trimmed[0]) + trimmed[1..];
    }
}