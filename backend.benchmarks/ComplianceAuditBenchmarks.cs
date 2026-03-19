using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace BankReporting.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ComplianceAuditBenchmarks
{
    private ComplianceAuditService _service = default!;
    private AuditTrailTraceRequest _traceRequest = default!;
    private AuditTrailQueryRequest _trailRequest = default!;

    [GlobalSetup]
    public void Setup()
    {
        _service = new ComplianceAuditService();
        var now = DateTime.UtcNow;

        for (var i = 0; i < 10_000; i++)
        {
            _service.RecordAuditTrail(new AuditTrailRecord
            {
                TimestampUtc = now.AddMilliseconds(-15_000 + i),
                TraceId = $"trace-{i % 600}",
                User = $"user-{i % 120}",
                Method = i % 3 == 0 ? "POST" : "GET",
                Path = i % 5 == 0 ? "/api/declare" : "/api/compliance/audit",
                StatusCode = i % 15 == 0 ? 500 : 200,
                DurationMs = 30 + (i % 900),
                RiskLevel = i % 8 == 0 ? "high" : (i % 3 == 0 ? "medium" : "low"),
                IsSensitiveOperation = i % 7 == 0
            });
        }

        for (var i = 0; i < 150; i++)
        {
            _service.RecordAuditTrail(new AuditTrailRecord
            {
                TimestampUtc = now.AddMilliseconds(-500 + i),
                TraceId = "trace-hot",
                User = "trace-user",
                Method = i % 2 == 0 ? "GET" : "POST",
                Path = "/api/compliance/audit/trace",
                StatusCode = 200,
                DurationMs = 20 + (i % 80),
                RiskLevel = "low",
                IsSensitiveOperation = i % 13 == 0
            });
        }

        _traceRequest = new AuditTrailTraceRequest
        {
            TraceId = "trace-hot",
            MaxSteps = 100
        };

        _trailRequest = new AuditTrailQueryRequest
        {
            Path = "/api/compliance",
            MinStatusCode = 200,
            StartDateUtc = now.AddMinutes(-20),
            EndDateUtc = now.AddMinutes(2),
            Page = 1,
            PageSize = 100
        };
    }

    [Benchmark]
    public AuditTrailTracePayload QueryTrace_ByTraceId() => _service.QueryTrace(_traceRequest);

    [Benchmark]
    public AuditTrailQueryPayload QueryTrails_Filtered() => _service.QueryTrails(_trailRequest);
}
