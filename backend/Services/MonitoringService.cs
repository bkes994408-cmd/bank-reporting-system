using System.Collections.Concurrent;
using System.Text;

namespace BankReporting.Api.Services;

public interface IMonitoringService
{
    void RecordRequest(string method, string path, int statusCode, long durationMs);
    string BuildPrometheusMetrics();
}

public class MonitoringService : IMonitoringService
{
    private long _totalRequests;
    private long _totalErrors;
    private long _totalDurationMs;

    private readonly ConcurrentDictionary<string, long> _requestsByRoute = new();
    private readonly ConcurrentDictionary<string, long> _errorsByRoute = new();

    public void RecordRequest(string method, string path, int statusCode, long durationMs)
    {
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Add(ref _totalDurationMs, durationMs);

        var routeKey = BuildRouteKey(method, path);
        _requestsByRoute.AddOrUpdate(routeKey, 1, (_, current) => current + 1);

        if (statusCode >= 500)
        {
            Interlocked.Increment(ref _totalErrors);
            _errorsByRoute.AddOrUpdate(routeKey, 1, (_, current) => current + 1);
        }
    }

    public string BuildPrometheusMetrics()
    {
        var totalRequests = Interlocked.Read(ref _totalRequests);
        var totalErrors = Interlocked.Read(ref _totalErrors);
        var totalDuration = Interlocked.Read(ref _totalDurationMs);
        var avgDuration = totalRequests == 0 ? 0 : (double)totalDuration / totalRequests;

        var sb = new StringBuilder();
        sb.AppendLine("# HELP bank_reporting_requests_total Total HTTP requests.");
        sb.AppendLine("# TYPE bank_reporting_requests_total counter");
        sb.AppendLine($"bank_reporting_requests_total {totalRequests}");

        sb.AppendLine("# HELP bank_reporting_errors_total Total HTTP 5xx responses.");
        sb.AppendLine("# TYPE bank_reporting_errors_total counter");
        sb.AppendLine($"bank_reporting_errors_total {totalErrors}");

        sb.AppendLine("# HELP bank_reporting_request_duration_ms_avg Average request duration in ms.");
        sb.AppendLine("# TYPE bank_reporting_request_duration_ms_avg gauge");
        sb.AppendLine($"bank_reporting_request_duration_ms_avg {avgDuration:F2}");

        sb.AppendLine("# HELP bank_reporting_route_requests_total Requests by route.");
        sb.AppendLine("# TYPE bank_reporting_route_requests_total counter");
        foreach (var metric in _requestsByRoute.OrderBy(x => x.Key))
        {
            sb.AppendLine($"bank_reporting_route_requests_total{{route=\"{metric.Key}\"}} {metric.Value}");
        }

        sb.AppendLine("# HELP bank_reporting_route_errors_total 5xx responses by route.");
        sb.AppendLine("# TYPE bank_reporting_route_errors_total counter");
        foreach (var metric in _errorsByRoute.OrderBy(x => x.Key))
        {
            sb.AppendLine($"bank_reporting_route_errors_total{{route=\"{metric.Key}\"}} {metric.Value}");
        }

        return sb.ToString();
    }

    private static string BuildRouteKey(string method, string path)
        => string.Concat(method, " ", NormalizePath(path));

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized;
    }
}
