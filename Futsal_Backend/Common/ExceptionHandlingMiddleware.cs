namespace Backend.Common;

/// <summary>Catches every unhandled exception and returns the { success, data, error } envelope.</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            await WriteAsync(context, ex.StatusCode, ApiResponse.Fail(ex.Code, ex.Message, ex.Details));
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected; nothing to send.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            var message = _environment.IsDevelopment()
                ? ex.GetBaseException().Message
                : "An unexpected error occurred. Please try again.";

            await WriteAsync(context, StatusCodes.Status500InternalServerError, ApiResponse.Fail("server_error", message));
        }
    }

    private static Task WriteAsync(HttpContext context, int statusCode, object body)
    {
        if (context.Response.HasStarted) return Task.CompletedTask;

        // Don't clear headers: CORS headers must survive so the browser can read the error.
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(body);
    }
}