using BankReporting.Api.Models;
using BankReporting.Api.Services;

namespace BankReporting.Api.Middleware;

public class RequestMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestMonitoringMiddleware> _logger;
    private readonly IMonitoringService _monitoringService;
    private readonly IComplianceAuditService _complianceAuditService;

    public RequestMonitoringMiddleware(
        RequestDelegate next,
        ILogger<RequestMonitoringMiddleware> logger,
        IMonitoringService monitoringService,
        IComplianceAuditService complianceAuditService)
    {
        _next = next;
        _logger = logger;
        _monitoringService = monitoringService;
        _complianceAuditService = complianceAuditService;
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
            _complianceAuditService.RecordAuditTrail(new AuditTrailRecord
            {
                TimestampUtc = startedAt,
                User = context.Request.Headers["X-User"].FirstOrDefault()?.Trim() ?? "anonymous",
                Role = context.Request.Headers["X-Role"].FirstOrDefault()?.Trim(),
                Method = method,
                Path = path,
                StatusCode = statusCode,
                DurationMs = durationMs,
                TraceId = context.TraceIdentifier,
                IsSensitiveOperation = IsSensitiveOperation(path, method),
                RiskLevel = ResolveRiskLevel(path, method, statusCode)
            });

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

    private static bool IsSensitiveOperation(string path, string method)
    {
        if (HttpMethods.IsPost(method) && (path.StartsWith("/api/declare", StringComparison.OrdinalIgnoreCase) ||
                                           path.StartsWith("/api/keys", StringComparison.OrdinalIgnoreCase) ||
                                           path.StartsWith("/api/token", StringComparison.OrdinalIgnoreCase) ||
                                           path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static string ResolveRiskLevel(string path, string method, int statusCode)
    {
        if (statusCode >= 500)
        {
            return "high";
        }

        if (statusCode >= 400)
        {
            return "medium";
        }

        if (IsSensitiveOperation(path, method))
        {
            return "medium";
        }

        return "low";
    }
}
