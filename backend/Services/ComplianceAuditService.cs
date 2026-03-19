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
    AuditDataIntegrityPayload CheckDataIntegrity(DataIntegrityCheckRequest request);
}

public class ComplianceAuditService : IComplianceAuditService
{
    private const int MaxTrailRecords = 10000;
    private const int MaxReportRecords = 2000;

    private readonly object _trailLock = new();
    private readonly object _reportLock = new();
    private readonly List<AuditTrailRecord> _trailRecords = [];
    private readonly Dictionary<string, Queue<AuditTrailRecord>> _traceIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ComplianceAuditReportRecord> _reports = [];

    public void RecordAuditTrail(AuditTrailRecord record)
    {
        lock (_trailLock)
        {
            _trailRecords.Add(record);
            AddTraceIndex(record);

            if (_trailRecords.Count > MaxTrailRecords)
            {
                var removeCount = _trailRecords.Count - MaxTrailRecords;
                for (var i = 0; i < removeCount; i++)
                {
                    var removed = _trailRecords[i];
                    RemoveTraceIndex(removed);
                }

                _trailRecords.RemoveRange(0, removeCount);
            }
        }
    }

    public Task<ComplianceAuditReportRecord> GenerateReportAsync(ComplianceAuditReportGenerateRequest request, CancellationToken cancellationToken)
    {
        var (start, end) = NormalizeRange(request.StartDateUtc, request.EndDateUtc, TimeSpan.FromDays(1));
        var records = SnapshotTrails()
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
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
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

        lock (_reportLock)
        {
            _reports.Add(report);
            if (_reports.Count > MaxReportRecords)
            {
                var removeCount = _reports.Count - MaxReportRecords;
                _reports.RemoveRange(0, removeCount);
            }
        }

        return Task.FromResult(report);
    }

    public ComplianceAuditReportsPayload QueryReports(ComplianceAuditReportQueryRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = SnapshotReports().AsEnumerable();
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

        var ordered = query
            .OrderByDescending(x => x.GeneratedAtUtc)
            .ThenByDescending(x => x.ReportId, StringComparer.Ordinal)
            .ToList();

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
        var skip = (page - 1) * pageSize;

        var (startUtc, endUtc) = NormalizeRange(request.StartDateUtc, request.EndDateUtc, TimeSpan.FromDays(7));
        var total = 0;
        var records = new List<AuditTrailRecord>(pageSize);

        // 由新到舊掃描，避免每次查詢都做 full sort。
        var snapshot = SnapshotTrails();
        for (var i = snapshot.Count - 1; i >= 0; i--)
        {
            var entry = snapshot[i];
            if (!IsTrailMatch(entry, request, startUtc, endUtc))
            {
                continue;
            }

            if (total >= skip && records.Count < pageSize)
            {
                records.Add(entry);
            }

            total++;
        }

        return new AuditTrailQueryPayload
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Records = records
        };
    }

    public AuditBehaviorInsightsPayload GetBehaviorInsights(AuditBehaviorInsightsRequest request)
    {
        var (start, end) = NormalizeRange(request.StartDateUtc, request.EndDateUtc, TimeSpan.FromDays(7));

        var records = SnapshotTrails()
            .Where(r => r.TimestampUtc >= start && r.TimestampUtc <= end)
            .ToList();

        var topUsers = Math.Clamp(request.TopUsers, 1, 20);
        var topPaths = Math.Clamp(request.TopPaths, 1, 20);

        var userSummaries = records
            .GroupBy(r => string.IsNullOrWhiteSpace(r.User) ? "anonymous" : r.User, StringComparer.OrdinalIgnoreCase)
            .Select(g => new AuditBehaviorUserSummary
            {
                User = g.Key,
                RequestCount = g.Count(),
                FailureCount = g.Count(x => x.StatusCode >= 400),
                SensitiveCount = g.Count(x => x.IsSensitiveOperation),
                AvgDurationMs = g.Any() ? (long)Math.Round(g.Average(x => x.DurationMs)) : 0
            })
            .OrderByDescending(x => x.RequestCount)
            .ThenByDescending(x => x.FailureCount)
            .ThenBy(x => x.User, StringComparer.OrdinalIgnoreCase)
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
            .ThenBy(x => x.PathKey, StringComparer.OrdinalIgnoreCase)
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
        var (startUtc, endUtc) = NormalizeRange(request.StartDateUtc, request.EndDateUtc, TimeSpan.FromDays(1));

        var source = SnapshotTraceSource(request.TraceId);
        var steps = source
            .Where(x => IsTraceMatch(x, request, startUtc, endUtc))
            .OrderBy(x => x.TimestampUtc)
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Method, StringComparer.OrdinalIgnoreCase)
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

    public AuditDataIntegrityPayload CheckDataIntegrity(DataIntegrityCheckRequest request)
    {
        var maxIssues = Math.Clamp(request.MaxIssues, 1, 1000);
        var trails = SnapshotTrails();
        var reports = SnapshotReports();

        var issues = new List<AuditDataIntegrityIssue>();
        var duplicateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var futureThreshold = DateTime.UtcNow.AddMinutes(5);

        foreach (var entry in trails)
        {
            if (issues.Count >= maxIssues)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(entry.Method) || string.IsNullOrWhiteSpace(entry.Path))
            {
                issues.Add(new AuditDataIntegrityIssue
                {
                    Type = "trail_required_field_missing",
                    Severity = "high",
                    Message = "audit trail 缺少必要欄位（method/path）",
                    RecordRef = BuildTrailRef(entry)
                });
            }

            if (entry.StatusCode is < 100 or > 599)
            {
                issues.Add(new AuditDataIntegrityIssue
                {
                    Type = "trail_status_code_invalid",
                    Severity = "medium",
                    Message = $"statusCode 不合法：{entry.StatusCode}",
                    RecordRef = BuildTrailRef(entry)
                });
            }

            if (entry.DurationMs < 0)
            {
                issues.Add(new AuditDataIntegrityIssue
                {
                    Type = "trail_duration_negative",
                    Severity = "medium",
                    Message = $"durationMs 不可為負值：{entry.DurationMs}",
                    RecordRef = BuildTrailRef(entry)
                });
            }

            if (entry.TimestampUtc > futureThreshold)
            {
                issues.Add(new AuditDataIntegrityIssue
                {
                    Type = "trail_timestamp_future",
                    Severity = "low",
                    Message = "timestampUtc 超出合理未來時間（> 5 分鐘）",
                    RecordRef = BuildTrailRef(entry)
                });
            }

            if (!string.IsNullOrWhiteSpace(entry.TraceId))
            {
                var key = $"{entry.TraceId}|{entry.TimestampUtc:O}|{entry.Method}|{entry.Path}";
                if (!duplicateKeys.Add(key))
                {
                    issues.Add(new AuditDataIntegrityIssue
                    {
                        Type = "trail_duplicate_possible",
                        Severity = "low",
                        Message = "偵測到疑似重複 audit trail 記錄",
                        RecordRef = BuildTrailRef(entry)
                    });
                }
            }
        }

        foreach (var report in reports)
        {
            if (issues.Count >= maxIssues)
            {
                break;
            }

            if (report.StartDateUtc > report.EndDateUtc)
            {
                issues.Add(new AuditDataIntegrityIssue
                {
                    Type = "report_range_invalid",
                    Severity = "high",
                    Message = "報告時間範圍異常：startDateUtc > endDateUtc",
                    RecordRef = report.ReportId
                });
            }

            if (report.Summary.TotalRequests < report.Summary.FailedRequests)
            {
                issues.Add(new AuditDataIntegrityIssue
                {
                    Type = "report_summary_inconsistent",
                    Severity = "high",
                    Message = "報告摘要異常：failedRequests > totalRequests",
                    RecordRef = report.ReportId
                });
            }

            if (report.Summary.TotalRequests < report.Summary.SensitiveOperations)
            {
                issues.Add(new AuditDataIntegrityIssue
                {
                    Type = "report_summary_inconsistent",
                    Severity = "high",
                    Message = "報告摘要異常：sensitiveOperations > totalRequests",
                    RecordRef = report.ReportId
                });
            }
        }

        return new AuditDataIntegrityPayload
        {
            GeneratedAtUtc = DateTime.UtcNow,
            TotalTrailRecords = trails.Count,
            TotalReportRecords = reports.Count,
            IsConsistent = issues.Count == 0,
            IssueCount = issues.Count,
            Issues = issues
        };
    }

    private List<AuditTrailRecord> SnapshotTrails()
    {
        lock (_trailLock)
        {
            return [.. _trailRecords];
        }
    }

    private List<ComplianceAuditReportRecord> SnapshotReports()
    {
        lock (_reportLock)
        {
            return [.. _reports];
        }
    }

    private List<AuditTrailRecord> SnapshotTraceSource(string? traceId)
    {
        lock (_trailLock)
        {
            if (!string.IsNullOrWhiteSpace(traceId) && _traceIndex.TryGetValue(traceId.Trim(), out var queue))
            {
                return [.. queue];
            }

            return [.. _trailRecords];
        }
    }

    private void AddTraceIndex(AuditTrailRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.TraceId))
        {
            return;
        }

        var key = record.TraceId.Trim();
        if (!_traceIndex.TryGetValue(key, out var queue))
        {
            queue = new Queue<AuditTrailRecord>();
            _traceIndex[key] = queue;
        }

        queue.Enqueue(record);
    }

    private void RemoveTraceIndex(AuditTrailRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.TraceId))
        {
            return;
        }

        var key = record.TraceId.Trim();
        if (!_traceIndex.TryGetValue(key, out var queue) || queue.Count == 0)
        {
            return;
        }

        // 正常情況下 queue 頭端就是要移除的最舊紀錄。
        if (ReferenceEquals(queue.Peek(), record))
        {
            queue.Dequeue();
        }
        else
        {
            var rebuilt = new Queue<AuditTrailRecord>(queue.Count);
            while (queue.TryDequeue(out var entry))
            {
                if (!ReferenceEquals(entry, record))
                {
                    rebuilt.Enqueue(entry);
                }
            }

            _traceIndex[key] = rebuilt;
            queue = rebuilt;
        }

        if (queue.Count == 0)
        {
            _traceIndex.Remove(key);
        }
    }

    private static bool IsTrailMatch(AuditTrailRecord entry, AuditTrailQueryRequest request, DateTime startUtc, DateTime endUtc)
    {
        if (!string.IsNullOrWhiteSpace(request.User) && !string.Equals(entry.User, request.User.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.Path) && !entry.Path.Contains(request.Path.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.RiskLevel) && !string.Equals(entry.RiskLevel, request.RiskLevel.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (request.SensitiveOnly == true && !entry.IsSensitiveOperation)
        {
            return false;
        }

        if (request.MinStatusCode.HasValue && entry.StatusCode < request.MinStatusCode.Value)
        {
            return false;
        }

        if (request.MaxStatusCode.HasValue && entry.StatusCode > request.MaxStatusCode.Value)
        {
            return false;
        }

        if (request.MinDurationMs.HasValue && entry.DurationMs < request.MinDurationMs.Value)
        {
            return false;
        }

        if (request.StartDateUtc.HasValue && entry.TimestampUtc < startUtc)
        {
            return false;
        }

        if (request.EndDateUtc.HasValue && entry.TimestampUtc > endUtc)
        {
            return false;
        }

        return true;
    }

    private static bool IsTraceMatch(AuditTrailRecord entry, AuditTrailTraceRequest request, DateTime startUtc, DateTime endUtc)
    {
        if (!string.IsNullOrWhiteSpace(request.TraceId) && !string.Equals(entry.TraceId, request.TraceId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.User) && !string.Equals(entry.User, request.User.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (request.StartDateUtc.HasValue && entry.TimestampUtc < startUtc)
        {
            return false;
        }

        if (request.EndDateUtc.HasValue && entry.TimestampUtc > endUtc)
        {
            return false;
        }

        return true;
    }

    private static (DateTime start, DateTime end) NormalizeRange(DateTime? startUtc, DateTime? endUtc, TimeSpan fallbackRange)
    {
        var end = endUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var start = startUtc?.ToUniversalTime() ?? end.Add(fallbackRange.Negate());
        if (start > end)
        {
            (start, end) = (end, start);
        }

        return (start, end);
    }

    private static string BuildTrailRef(AuditTrailRecord entry)
        => $"{entry.TimestampUtc:O}|{entry.TraceId}|{entry.Method}|{entry.Path}";

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
