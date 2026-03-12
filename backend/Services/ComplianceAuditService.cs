using System.Collections.Concurrent;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IComplianceAuditService
{
    void RecordAuditTrail(AuditTrailRecord record);
    Task<ComplianceAuditReportRecord> GenerateReportAsync(ComplianceAuditReportGenerateRequest request, CancellationToken cancellationToken);
    ComplianceAuditReportsPayload QueryReports(ComplianceAuditReportQueryRequest request);
    AuditTrailQueryPayload QueryTrails(AuditTrailQueryRequest request);
    AuditBehaviorInsightsPayload GetBehaviorInsights(AuditBehaviorInsightsRequest request);
    AuditTrailTracePayload QueryTrace(AuditTrailTraceRequest request);
}

public class ComplianceAuditService : IComplianceAuditService
{
    private readonly ConcurrentQueue<AuditTrailRecord> _trailRecords = new();
    private readonly ConcurrentQueue<ComplianceAuditReportRecord> _reports = new();

    public void RecordAuditTrail(AuditTrailRecord record)
    {
        _trailRecords.Enqueue(record);
        while (_trailRecords.Count > 10000 && _trailRecords.TryDequeue(out _))
        {
        }
    }

    public Task<ComplianceAuditReportRecord> GenerateReportAsync(ComplianceAuditReportGenerateRequest request, CancellationToken cancellationToken)
    {
        var end = request.EndDateUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var start = request.StartDateUtc?.ToUniversalTime() ?? end.AddDays(-1);

        if (start > end)
        {
            (start, end) = (end, start);
        }

        var records = _trailRecords
            .Where(r => r.TimestampUtc >= start && r.TimestampUtc <= end)
            .ToList();

        var summary = new ComplianceAuditSummary
        {
            TotalRequests = records.Count,
            FailedRequests = records.Count(r => r.StatusCode >= 400),
            SensitiveOperations = records.Count(r => r.IsSensitiveOperation),
            HighRiskOperations = records.Count(r => string.Equals(r.RiskLevel, "high", StringComparison.OrdinalIgnoreCase)),
            UniqueUsers = records.Select(r => r.User).Distinct(StringComparer.OrdinalIgnoreCase).Count()
        };

        var topSensitiveEndpoints = records
            .Where(r => r.IsSensitiveOperation)
            .GroupBy(r => $"{r.Method} {r.Path}")
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        var report = new ComplianceAuditReportRecord
        {
            ReportId = $"audit-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32],
            GeneratedAtUtc = DateTime.UtcNow,
            StartDateUtc = start,
            EndDateUtc = end,
            Summary = summary,
            TopSensitiveEndpoints = topSensitiveEndpoints
        };

        _reports.Enqueue(report);
        while (_reports.Count > 2000 && _reports.TryDequeue(out _))
        {
        }

        return Task.FromResult(report);
    }

    public ComplianceAuditReportsPayload QueryReports(ComplianceAuditReportQueryRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _reports.AsEnumerable();
        if (request.FromGeneratedAtUtc.HasValue)
        {
            var fromUtc = request.FromGeneratedAtUtc.Value.ToUniversalTime();
            query = query.Where(x => x.GeneratedAtUtc >= fromUtc);
        }

        if (request.ToGeneratedAtUtc.HasValue)
        {
            var toUtc = request.ToGeneratedAtUtc.Value.ToUniversalTime();
            query = query.Where(x => x.GeneratedAtUtc <= toUtc);
        }

        var ordered = query.OrderByDescending(x => x.GeneratedAtUtc).ToList();
        return new ComplianceAuditReportsPayload
        {
            Total = ordered.Count,
            Page = page,
            PageSize = pageSize,
            Reports = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList()
        };
    }

    public AuditTrailQueryPayload QueryTrails(AuditTrailQueryRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);

        var query = _trailRecords.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(request.User))
        {
            var user = request.User.Trim();
            query = query.Where(x => string.Equals(x.User, user, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Path))
        {
            var path = request.Path.Trim();
            query = query.Where(x => x.Path.Contains(path, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.RiskLevel))
        {
            var risk = request.RiskLevel.Trim();
            query = query.Where(x => string.Equals(x.RiskLevel, risk, StringComparison.OrdinalIgnoreCase));
        }

        if (request.SensitiveOnly == true)
        {
            query = query.Where(x => x.IsSensitiveOperation);
        }

        if (request.MinStatusCode.HasValue)
        {
            query = query.Where(x => x.StatusCode >= request.MinStatusCode.Value);
        }

        if (request.MaxStatusCode.HasValue)
        {
            query = query.Where(x => x.StatusCode <= request.MaxStatusCode.Value);
        }

        if (request.MinDurationMs.HasValue)
        {
            query = query.Where(x => x.DurationMs >= request.MinDurationMs.Value);
        }

        if (request.StartDateUtc.HasValue)
        {
            var startUtc = request.StartDateUtc.Value.ToUniversalTime();
            query = query.Where(x => x.TimestampUtc >= startUtc);
        }

        if (request.EndDateUtc.HasValue)
        {
            var endUtc = request.EndDateUtc.Value.ToUniversalTime();
            query = query.Where(x => x.TimestampUtc <= endUtc);
        }

        var ordered = query.OrderByDescending(x => x.TimestampUtc).ToList();
        return new AuditTrailQueryPayload
        {
            Total = ordered.Count,
            Page = page,
            PageSize = pageSize,
            Records = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList()
        };
    }

    public AuditBehaviorInsightsPayload GetBehaviorInsights(AuditBehaviorInsightsRequest request)
    {
        var end = request.EndDateUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var start = request.StartDateUtc?.ToUniversalTime() ?? end.AddDays(-7);

        if (start > end)
        {
            (start, end) = (end, start);
        }

        var records = _trailRecords
            .Where(r => r.TimestampUtc >= start && r.TimestampUtc <= end)
            .ToList();

        var topUsers = Math.Clamp(request.TopUsers, 1, 20);
        var topPaths = Math.Clamp(request.TopPaths, 1, 20);

        var userSummaries = records
            .GroupBy(r => string.IsNullOrWhiteSpace(r.User) ? "anonymous" : r.User, StringComparer.OrdinalIgnoreCase)
            .Select(g => new AuditBehaviorUserSummary
            {
                User = g.First().User,
                RequestCount = g.Count(),
                FailureCount = g.Count(x => x.StatusCode >= 400),
                SensitiveCount = g.Count(x => x.IsSensitiveOperation),
                AvgDurationMs = g.Any() ? (long)Math.Round(g.Average(x => x.DurationMs)) : 0
            })
            .OrderByDescending(x => x.RequestCount)
            .ThenByDescending(x => x.FailureCount)
            .Take(topUsers)
            .ToList();

        var pathSummaries = records
            .GroupBy(r => $"{r.Method} {r.Path}")
            .Select(g => new AuditBehaviorPathSummary
            {
                PathKey = g.Key,
                RequestCount = g.Count(),
                FailureCount = g.Count(x => x.StatusCode >= 400),
                AvgDurationMs = g.Any() ? (long)Math.Round(g.Average(x => x.DurationMs)) : 0
            })
            .OrderByDescending(x => x.RequestCount)
            .ThenByDescending(x => x.FailureCount)
            .Take(topPaths)
            .ToList();

        return new AuditBehaviorInsightsPayload
        {
            GeneratedAtUtc = DateTime.UtcNow,
            StartDateUtc = start,
            EndDateUtc = end,
            TotalRecords = records.Count,
            UniqueUsers = records
                .Select(r => string.IsNullOrWhiteSpace(r.User) ? "anonymous" : r.User)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            SensitiveOperations = records.Count(r => r.IsSensitiveOperation),
            TopActiveUsers = userSummaries,
            TopPaths = pathSummaries,
            OptimizationSuggestions = BuildOptimizationSuggestions(records)
        };
    }

    public AuditTrailTracePayload QueryTrace(AuditTrailTraceRequest request)
    {
        var maxSteps = Math.Clamp(request.MaxSteps, 1, 200);
        var query = _trailRecords.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.TraceId))
        {
            var traceId = request.TraceId.Trim();
            query = query.Where(x => string.Equals(x.TraceId, traceId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.User))
        {
            var user = request.User.Trim();
            query = query.Where(x => string.Equals(x.User, user, StringComparison.OrdinalIgnoreCase));
        }

        if (request.StartDateUtc.HasValue)
        {
            var startUtc = request.StartDateUtc.Value.ToUniversalTime();
            query = query.Where(x => x.TimestampUtc >= startUtc);
        }

        if (request.EndDateUtc.HasValue)
        {
            var endUtc = request.EndDateUtc.Value.ToUniversalTime();
            query = query.Where(x => x.TimestampUtc <= endUtc);
        }

        var steps = query
            .OrderBy(x => x.TimestampUtc)
            .Take(maxSteps)
            .Select(x => new AuditTrailTraceStep
            {
                TimestampUtc = x.TimestampUtc,
                TraceId = x.TraceId,
                User = x.User,
                Method = x.Method,
                Path = x.Path,
                StatusCode = x.StatusCode,
                DurationMs = x.DurationMs,
                RiskLevel = x.RiskLevel
            })
            .ToList();

        return new AuditTrailTracePayload
        {
            TraceId = request.TraceId?.Trim() ?? (steps.FirstOrDefault()?.TraceId ?? string.Empty),
            User = request.User?.Trim() ?? (steps.FirstOrDefault()?.User ?? string.Empty),
            StartDateUtc = request.StartDateUtc?.ToUniversalTime(),
            EndDateUtc = request.EndDateUtc?.ToUniversalTime(),
            TotalSteps = steps.Count,
            Steps = steps
        };
    }

    private static List<string> BuildOptimizationSuggestions(List<AuditTrailRecord> records)
    {
        var suggestions = new List<string>();
        if (records.Count == 0)
        {
            suggestions.Add("目前時窗內沒有稽核資料，建議先確認 audit trail 留存設定與流量來源。");
            return suggestions;
        }

        var failureRate = records.Count(x => x.StatusCode >= 400) / (double)records.Count;
        if (failureRate >= 0.2)
        {
            suggestions.Add($"失敗率偏高（{failureRate:P0}），建議優先排查高失敗端點並建立重試/回退策略。");
        }

        var slowRate = records.Count(x => x.DurationMs >= 2000) / (double)records.Count;
        if (slowRate >= 0.15)
        {
            suggestions.Add($"慢請求比例偏高（{slowRate:P0}），建議針對 Top Paths 進行效能分析與快取優化。");
        }

        var sensitiveRate = records.Count(x => x.IsSensitiveOperation) / (double)records.Count;
        if (sensitiveRate >= 0.3)
        {
            suggestions.Add($"敏感操作占比高（{sensitiveRate:P0}），建議加強雙人覆核、MFA 與最小權限檢視。");
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add("整體行為分布穩定，建議持續監控 Top Users / Top Paths 並定期回顧規則閾值。");
        }

        return suggestions;
    }
}
