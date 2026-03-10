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
}
