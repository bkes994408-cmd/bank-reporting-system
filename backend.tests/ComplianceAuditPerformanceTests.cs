using System.Collections.Concurrent;
using System.Diagnostics;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Xunit;

namespace BankReporting.Tests;

public class ComplianceAuditPerformanceTests
{
    [Fact]
    public void QueryTrace_Benchmark_BaselineUnderLoad()
    {
        var service = BuildServiceWithSeedData(totalRecords: 10_000, targetTraceId: "trace-hot", targetTraceCount: 120);
        var request = new AuditTrailTraceRequest
        {
            TraceId = "trace-hot",
            MaxSteps = 100
        };

        const int iterations = 1_000;
        var elapsedMs = new long[iterations];

        for (var i = 0; i < iterations; i++)
        {
            var start = Stopwatch.GetTimestamp();
            var payload = service.QueryTrace(request);
            elapsedMs[i] = (long)(Stopwatch.GetElapsedTime(start).TotalMilliseconds * 1000); // microseconds

            Assert.True(payload.TotalSteps <= request.MaxSteps);
            Assert.All(payload.Steps, step => Assert.Equal("trace-hot", step.TraceId));
            Assert.True(IsSortedByTimestamp(payload.Steps));
        }

        Array.Sort(elapsedMs);
        var p95Us = elapsedMs[(int)Math.Floor(iterations * 0.95) - 1];
        var avgUs = elapsedMs.Average();

        // 閥值以 CI 可穩定通過為主，避免單機 jitter 造成假陽性。
        Assert.True(avgUs < 3_000, $"avg latency too high: {avgUs:F0}us");
        Assert.True(p95Us < 6_000, $"p95 latency too high: {p95Us}us");
    }

    [Fact]
    public async Task QueryTrace_StressTest_RemainsStableUnderConcurrentReadWrite()
    {
        var service = BuildServiceWithSeedData(totalRecords: 8_000, targetTraceId: "trace-load", targetTraceCount: 80);
        var errors = new ConcurrentQueue<Exception>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        var readers = Enumerable.Range(0, 12).Select(_ => Task.Run(() =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var payload = service.QueryTrace(new AuditTrailTraceRequest
                    {
                        TraceId = "trace-load",
                        MaxSteps = 100,
                        StartDateUtc = DateTime.UtcNow.AddMinutes(-30),
                        EndDateUtc = DateTime.UtcNow.AddMinutes(5)
                    });

                    if (!IsSortedByTimestamp(payload.Steps))
                    {
                        throw new Xunit.Sdk.XunitException("trace steps are not sorted by TimestampUtc");
                    }

                    if (payload.TotalSteps > 100)
                    {
                        throw new Xunit.Sdk.XunitException($"trace steps exceed maxSteps: {payload.TotalSteps}");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Enqueue(ex);
            }
        }, cts.Token));

        var writers = Enumerable.Range(0, 4).Select(writerId => Task.Run(async () =>
        {
            try
            {
                var seq = 0;
                while (!cts.IsCancellationRequested)
                {
                    var now = DateTime.UtcNow;
                    service.RecordAuditTrail(new AuditTrailRecord
                    {
                        TimestampUtc = now,
                        TraceId = "trace-load",
                        User = $"writer-{writerId}",
                        Method = seq % 2 == 0 ? "GET" : "POST",
                        Path = "/api/compliance/audit/trace",
                        StatusCode = 200,
                        DurationMs = 20 + (seq % 200),
                        RiskLevel = seq % 7 == 0 ? "high" : "low",
                        IsSensitiveOperation = seq % 5 == 0
                    });
                    seq++;

                    await Task.Delay(2, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // expected
            }
            catch (Exception ex)
            {
                errors.Enqueue(ex);
            }
        }, cts.Token));

        await Task.WhenAll(readers.Concat(writers));

        Assert.True(errors.IsEmpty, string.Join("\n", errors.Select(x => x.ToString())));

        var finalPayload = service.QueryTrace(new AuditTrailTraceRequest
        {
            TraceId = "trace-load",
            MaxSteps = 200
        });

        Assert.True(finalPayload.TotalSteps > 0);
        Assert.True(IsSortedByTimestamp(finalPayload.Steps));
    }

    private static ComplianceAuditService BuildServiceWithSeedData(int totalRecords, string targetTraceId, int targetTraceCount)
    {
        var service = new ComplianceAuditService();
        var now = DateTime.UtcNow;

        for (var i = 0; i < totalRecords; i++)
        {
            service.RecordAuditTrail(new AuditTrailRecord
            {
                TimestampUtc = now.AddSeconds(-(totalRecords - i)),
                TraceId = $"trace-{i % 250}",
                User = $"user-{i % 80}",
                Method = i % 3 == 0 ? "POST" : "GET",
                Path = i % 4 == 0 ? "/api/declare" : "/api/compliance/audit",
                StatusCode = i % 20 == 0 ? 500 : 200,
                DurationMs = 40 + (i % 900),
                RiskLevel = i % 10 == 0 ? "high" : (i % 3 == 0 ? "medium" : "low"),
                IsSensitiveOperation = i % 6 == 0
            });
        }

        for (var i = 0; i < targetTraceCount; i++)
        {
            service.RecordAuditTrail(new AuditTrailRecord
            {
                TimestampUtc = now.AddMilliseconds(-targetTraceCount + i),
                TraceId = targetTraceId,
                User = "trace-user",
                Method = i % 2 == 0 ? "GET" : "POST",
                Path = "/api/compliance/audit/trace",
                StatusCode = 200,
                DurationMs = 25 + (i % 30),
                RiskLevel = "low",
                IsSensitiveOperation = i % 11 == 0
            });
        }

        return service;
    }

    private static bool IsSortedByTimestamp(IReadOnlyList<AuditTrailTraceStep> steps)
    {
        for (var i = 1; i < steps.Count; i++)
        {
            if (steps[i].TimestampUtc < steps[i - 1].TimestampUtc)
            {
                return false;
            }
        }

        return true;
    }
}
