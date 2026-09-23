namespace Backend.Common;

/// <summary>Throw from services; the middleware turns it into the JSON error envelope.</summary>
public class AppException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }
    public Dictionary<string, string[]>? Details { get; }

    public AppException(int statusCode, string code, string message, Dictionary<string, string[]>? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Details = details;
    }

    public static AppException BadRequest(string message, string code = "bad_request") =>
        new(StatusCodes.Status400BadRequest, code, message);

    public static AppException Validation(string field, string message) =>
        new(StatusCodes.Status400BadRequest, "validation_error", message,
            new Dictionary<string, string[]> { [field] = new[] { message } });

    public static AppException Unauthorized(string message, string code = "unauthorized") =>
        new(StatusCodes.Status401Unauthorized, code, message);

    public static AppException Forbidden(string message, string code = "forbidden") =>
        new(StatusCodes.Status403Forbidden, code, message);

    public static AppException NotFound(string message, string code = "not_found") =>
        new(StatusCodes.Status404NotFound, code, message);

    public static AppException Conflict(string message, string code = "conflict") =>
        new(StatusCodes.Status409Conflict, code, message);

    public static AppException BadGateway(string message, string code = "gateway_error") =>
        new(StatusCodes.Status502BadGateway, code, message);
}