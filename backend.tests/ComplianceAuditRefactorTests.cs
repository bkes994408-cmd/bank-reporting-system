using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Xunit;

namespace BankReporting.Tests;

public class ComplianceAuditRefactorTests
{
    [Fact]
    public void InMemoryRepository_TrimsOldRecords_AndKeepsTraceIndexUsable()
    {
        var repository = new InMemoryComplianceAuditRepository();

        for (var i = 0; i < 10005; i++)
        {
            repository.AddTrail(new AuditTrailRecord
            {
                TimestampUtc = DateTime.UtcNow.AddSeconds(-10005 + i),
                TraceId = "trace-trim",
                Method = "GET",
                Path = $"/p/{i}",
                StatusCode = 200
            });
        }

        var trails = repository.SnapshotTrails();
        Assert.Equal(10000, trails.Count);
        Assert.DoesNotContain(trails, x => x.Path == "/p/0");

        var traceRecords = repository.SnapshotTraceSource("trace-trim");
        Assert.Equal(10000, traceRecords.Count);
    }

    [Fact]
    public async Task QueryReports_AppliesDateRangeAndPaging()
    {
        var service = new ComplianceAuditService();
        var now = DateTime.UtcNow;

        for (var i = 0; i < 3; i++)
        {
            service.RecordAuditTrail(new AuditTrailRecord
            {
                TimestampUtc = now.AddMinutes(-(i + 1)),
                User = $"u{i}",
                Method = "GET",
                Path = "/api/r",
                StatusCode = 200,
                IsSensitiveOperation = false,
                RiskLevel = "low"
            });

            await service.GenerateReportAsync(new ComplianceAuditReportGenerateRequest
            {
                StartDateUtc = now.AddHours(-1),
                EndDateUtc = now
            }, CancellationToken.None);
        }

        var result = service.QueryReports(new ComplianceAuditReportQueryRequest
        {
            FromGeneratedAtUtc = now.AddMinutes(-10),
            ToGeneratedAtUtc = DateTime.UtcNow.AddMinutes(1),
            Page = 1,
            PageSize = 2
        });

        Assert.Equal(2, result.Reports.Count);
        Assert.True(result.Total >= 2);
    }

    [Fact]
    public void InMemoryRepository_HandlesReportTrim_AndTraceFallback()
    {
        var repository = new InMemoryComplianceAuditRepository();
        for (var i = 0; i < 2005; i++)
        {
            repository.AddReport(new ComplianceAuditReportRecord { ReportId = $"r{i}", GeneratedAtUtc = DateTime.UtcNow });
        }

        var reports = repository.SnapshotReports();
        Assert.Equal(2000, reports.Count);
        Assert.DoesNotContain(reports, x => x.ReportId == "r0");

        repository.AddTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow,
            TraceId = "   ",
            Method = "GET",
            Path = "/no-trace",
            StatusCode = 200
        });

        var fromMissingTrace = repository.SnapshotTraceSource("not-exists");
        Assert.Single(fromMissingTrace);
    }

    [Fact]
    public void GetBehaviorInsights_ReturnsStableSuggestion_WhenNoRiskSignals()
    {
        var service = new ComplianceAuditService();
        var now = DateTime.UtcNow;

        service.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = now.AddMinutes(-5),
            User = "alice",
            Method = "GET",
            Path = "/api/reports",
            StatusCode = 200,
            DurationMs = 100,
            IsSensitiveOperation = false,
            RiskLevel = "low"
        });

        var payload = service.GetBehaviorInsights(new AuditBehaviorInsightsRequest
        {
            StartDateUtc = now.AddHours(-1),
            EndDateUtc = now,
            TopUsers = 1,
            TopPaths = 1
        });

        Assert.Contains(payload.OptimizationSuggestions, x => x.Contains("整體行為分布穩定"));
    }

    [Fact]
    public void QueryTrace_ReturnsEmptyNotes_WhenNoMatchedRecords()
    {
        var service = new ComplianceAuditService();
        var payload = service.QueryTrace(new AuditTrailTraceRequest { TraceId = "missing", MaxSteps = 5 });

        Assert.Equal(0, payload.TotalSteps);
        Assert.Contains(payload.ExplainabilityNotes, x => x.Contains("查無符合條件"));
    }

    [Fact]
    public void CheckDataIntegrity_DetectsFutureTimestampAndDuplicateTrace()
    {
        var service = new ComplianceAuditService();
        var ts = DateTime.UtcNow.AddMinutes(6);

        service.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = ts,
            TraceId = "dup-1",
            Method = "GET",
            Path = "/api/p",
            StatusCode = 200
        });
        service.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = ts,
            TraceId = "dup-1",
            Method = "GET",
            Path = "/api/p",
            StatusCode = 200
        });

        var payload = service.CheckDataIntegrity(new DataIntegrityCheckRequest { MaxIssues = 20 });

        Assert.Contains(payload.Issues, x => x.Type == "trail_timestamp_future");
        Assert.Contains(payload.Issues, x => x.Type == "trail_duplicate_possible");
    }

    [Fact]
    public void QueryTrace_FallbackToSortedOrder_WhenSourceOutOfOrder()
    {
        var repository = new FakeAuditRepository
        {
            TraceSource =
            [
                new AuditTrailRecord { TimestampUtc = DateTime.UtcNow.AddMinutes(-1), TraceId = "t-1", User = "u", Method = "POST", Path = "/b", StatusCode = 200 },
                new AuditTrailRecord { TimestampUtc = DateTime.UtcNow.AddMinutes(-5), TraceId = "t-1", User = "u", Method = "GET", Path = "/a", StatusCode = 500 }
            ]
        };

        var service = new ComplianceAuditService(repository);
        var payload = service.QueryTrace(new AuditTrailTraceRequest { TraceId = "t-1", MaxSteps = 10 });

        Assert.Equal(2, payload.TotalSteps);
        Assert.True(payload.Steps[0].TimestampUtc <= payload.Steps[1].TimestampUtc);
    }

    [Fact]
    public void CheckDataIntegrity_DetectsReportLevelInconsistencyAndRangeIssue()
    {
        var repository = new FakeAuditRepository
        {
            Reports =
            [
                new ComplianceAuditReportRecord
                {
                    ReportId = "r1",
                    StartDateUtc = DateTime.UtcNow,
                    EndDateUtc = DateTime.UtcNow.AddHours(-1),
                    Summary = new ComplianceAuditSummary { TotalRequests = 1, FailedRequests = 2, SensitiveOperations = 3 }
                }
            ]
        };

        var service = new ComplianceAuditService(repository);
        var payload = service.CheckDataIntegrity(new DataIntegrityCheckRequest { MaxIssues = 20 });

        Assert.Contains(payload.Issues, x => x.Type == "report_range_invalid");
        Assert.True(payload.Issues.Count(x => x.Type == "report_summary_inconsistent") >= 2);
    }

    [Fact]
    public void QueryTrails_Unfiltered_UsesReversePagingContract()
    {
        var service = new ComplianceAuditService();
        var now = DateTime.UtcNow;

        for (var i = 0; i < 12; i++)
        {
            service.RecordAuditTrail(new AuditTrailRecord
            {
                TimestampUtc = now.AddSeconds(i),
                User = $"u{i}",
                Method = "GET",
                Path = $"/p/{i}",
                StatusCode = 200
            });
        }

        var page2 = service.QueryTrails(new AuditTrailQueryRequest { Page = 2, PageSize = 5 });

        Assert.Equal(12, page2.Total);
        Assert.Equal(5, page2.Records.Count);
        Assert.Equal("/p/6", page2.Records[0].Path);
        Assert.Equal("/p/2", page2.Records[^1].Path);
    }

    [Fact]
    public async Task GenerateReportAsync_PersistsToRepository()
    {
        var now = DateTime.UtcNow;
        var repository = new FakeAuditRepository
        {
            Trails =
            [
                new AuditTrailRecord { TimestampUtc = now.AddMinutes(-1), User = "alice", Method = "GET", Path = "/api/r", StatusCode = 200, IsSensitiveOperation = false, RiskLevel = "low" }
            ]
        };

        var service = new ComplianceAuditService(repository);
        var report = await service.GenerateReportAsync(new ComplianceAuditReportGenerateRequest(), CancellationToken.None);

        Assert.Single(repository.AddedReports);
        Assert.Equal(report.ReportId, repository.AddedReports[0].ReportId);
    }

    [Fact]
    public void CheckDataIntegrity_DetectsTraceInconsistencyAndInvalidRiskLevel()
    {
        var service = new ComplianceAuditService();

        service.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-2),
            TraceId = "trace-x",
            User = "alice",
            Method = "GET",
            Path = "/api/a",
            StatusCode = 200,
            RiskLevel = "unknown"
        });

        service.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-1),
            TraceId = "trace-x",
            User = "bob",
            Method = "POST",
            Path = "/api/b",
            StatusCode = 200,
            RiskLevel = "low"
        });

        var payload = service.CheckDataIntegrity(new DataIntegrityCheckRequest { MaxIssues = 20 });

        Assert.Contains(payload.Issues, x => x.Type == "trail_risk_level_invalid");
        Assert.Contains(payload.Issues, x => x.Type == "trail_trace_user_inconsistent");
        Assert.Contains(payload.Issues, x => x.Type == "trail_trace_path_inconsistent");
    }

    private sealed class FakeAuditRepository : IComplianceAuditRepository
    {
        public List<AuditTrailRecord> Trails { get; set; } = [];
        public List<AuditTrailRecord> TraceSource { get; set; } = [];
        public List<ComplianceAuditReportRecord> Reports { get; set; } = [];
        public List<ComplianceAuditReportRecord> AddedReports { get; } = [];

        public void AddTrail(AuditTrailRecord record) => Trails.Add(record);
        public List<AuditTrailRecord> SnapshotTrails() => [.. Trails];
        public List<AuditTrailRecord> SnapshotTraceSource(string? traceId) => TraceSource.Count == 0 ? [.. Trails] : [.. TraceSource];
        public void AddReport(ComplianceAuditReportRecord report)
        {
            AddedReports.Add(report);
            Reports.Add(report);
        }

        public List<ComplianceAuditReportRecord> SnapshotReports() => [.. Reports];
    }
}
