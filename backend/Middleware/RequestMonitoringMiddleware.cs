using BankReporting.Api.Services;

namespace BankReporting.Api.Middleware;

public class RequestMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestMonitoringMiddleware> _logger;
    private readonly IMonitoringService _monitoringService;

    public RequestMonitoringMiddleware(
        RequestDelegate next,
        ILogger<RequestMonitoringMiddleware> logger,
        IMonitoringService monitoringService)
    {
        _next = next;
        _logger = logger;
        _monitoringService = monitoringService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = DateTime.UtcNow;
        try
        {
            await _next(context);
        }
        finally
        {
            var durationMs = (long)(DateTime.UtcNow - startedAt).TotalMilliseconds;
            var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
            var method = context.Request.Method;
            var statusCode = context.Response.StatusCode;

            _monitoringService.RecordRequest(method, path, statusCode, durationMs);

            _logger.LogInformation("HTTP {Method} {Path} => {StatusCode} ({DurationMs}ms)",
                method, path, statusCode, durationMs);

            if (statusCode >= 500)
            {
                _logger.LogWarning("ALERT: High-severity API error detected. {Method} {Path} => {StatusCode}",
                    method, path, statusCode);
            }

            if (durationMs >= 2000)
            {
                _logger.LogWarning("ALERT: Slow API response detected. {Method} {Path} took {DurationMs}ms",
                    method, path, durationMs);
            }
        }
    }
}
