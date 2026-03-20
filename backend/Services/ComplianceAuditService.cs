using System.Text;
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
    private readonly IComplianceAuditRepository _repository;

    public ComplianceAuditService()
        : this(new InMemoryComplianceAuditRepository())
    {
    }

    internal ComplianceAuditService(IComplianceAuditRepository repository)
    {
        _repository = repository;
    }

    public void RecordAuditTrail(AuditTrailRecord record)
        => _repository.AddTrail(record);

    public Task<ComplianceAuditReportRecord> GenerateReportAsync(ComplianceAuditReportGenerateRequest request, CancellationToken cancellationToken)
    {
        var (start, end) = NormalizeRange(request.StartDateUtc, request.EndDateUtc, TimeSpan.FromDays(1));
        var records = _repository.SnapshotTrails()
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

        _repository.AddReport(report);

        return Task.FromResult(report);
    }

    public ComplianceAuditReportsPayload QueryReports(ComplianceAuditReportQueryRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _repository.SnapshotReports().AsEnumerable();
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

        var snapshot = _repository.SnapshotTrails();
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
        var snapshot = _repository.SnapshotTrails();

        var topUsers = Math.Clamp(request.TopUsers, 1, 20);
        var topPaths = Math.Clamp(request.TopPaths, 1, 20);

        var totalRecords = 0;
        var sensitiveOperations = 0;
        var failedRecords = 0;
        var slowRecords = 0;

        var userStats = new Dictionary<string, AggregateStats>(StringComparer.OrdinalIgnoreCase);
        var pathStats = new Dictionary<string, AggregateStats>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in snapshot)
        {
            if (record.TimestampUtc < start || record.TimestampUtc > end)
            {
                continue;
            }

            totalRecords++;
            if (record.IsSensitiveOperation)
            {
                sensitiveOperations++;
            }

            if (record.StatusCode >= 400)
            {
                failedRecords++;
            }

            if (record.DurationMs >= 2000)
            {
                slowRecords++;
            }

            var userKey = string.IsNullOrWhiteSpace(record.User) ? "anonymous" : record.User;
            UpdateAggregate(userStats, userKey, record);
            UpdateAggregate(pathStats, $"{record.Method} {record.Path}", record);
        }

        var userSummaries = userStats
            .Select(x => new AuditBehaviorUserSummary
            {
                User = x.Key,
                RequestCount = x.Value.Count,
                FailureCount = x.Value.Failures,
                SensitiveCount = x.Value.Sensitive,
                AvgDurationMs = x.Value.Count == 0 ? 0 : (long)Math.Round((double)x.Value.TotalDuration / x.Value.Count)
            })
            .OrderByDescending(x => x.RequestCount)
            .ThenByDescending(x => x.FailureCount)
            .ThenBy(x => x.User, StringComparer.OrdinalIgnoreCase)
            .Take(topUsers)
            .ToList();

        var pathSummaries = pathStats
            .Select(x => new AuditBehaviorPathSummary
            {
                PathKey = x.Key,
                RequestCount = x.Value.Count,
                FailureCount = x.Value.Failures,
                AvgDurationMs = x.Value.Count == 0 ? 0 : (long)Math.Round((double)x.Value.TotalDuration / x.Value.Count)
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
            TotalRecords = totalRecords,
            UniqueUsers = userStats.Count,
            SensitiveOperations = sensitiveOperations,
            TopActiveUsers = userSummaries,
            TopPaths = pathSummaries,
            OptimizationSuggestions = BuildOptimizationSuggestions(totalRecords, failedRecords, slowRecords, sensitiveOperations, pathSummaries)
        };
    }

    public AuditTrailTracePayload QueryTrace(AuditTrailTraceRequest request)
    {
        var maxSteps = Math.Clamp(request.MaxSteps, 1, 200);
        var (startUtc, endUtc) = NormalizeRange(request.StartDateUtc, request.EndDateUtc, TimeSpan.FromDays(1));

        var source = _repository.SnapshotTraceSource(request.TraceId);
        var steps = new List<AuditTrailTraceStep>(maxSteps);

        foreach (var item in source)
        {
            if (!IsTraceMatch(item, request, startUtc, endUtc))
            {
                continue;
            }

            steps.Add(new AuditTrailTraceStep
            {
                TimestampUtc = item.TimestampUtc,
                TraceId = item.TraceId,
                User = item.User,
                Method = item.Method,
                Path = item.Path,
                StatusCode = item.StatusCode,
                DurationMs = item.DurationMs,
                RiskLevel = item.RiskLevel
            });

            if (steps.Count >= maxSteps)
            {
                break;
            }
        }

        if (!IsSortedByTimestamp(steps))
        {
            steps = steps
                .OrderBy(x => x.TimestampUtc)
                .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Method, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var visualization = BuildTraceVisualization(steps);

        return new AuditTrailTracePayload
        {
            TraceId = request.TraceId?.Trim() ?? (steps.FirstOrDefault()?.TraceId ?? string.Empty),
            User = request.User?.Trim() ?? (steps.FirstOrDefault()?.User ?? string.Empty),
            StartDateUtc = request.StartDateUtc?.ToUniversalTime(),
            EndDateUtc = request.EndDateUtc?.ToUniversalTime(),
            TotalSteps = steps.Count,
            Steps = steps,
            Visualization = visualization,
            ExplainabilityNotes = BuildTraceExplainabilityNotes(steps)
        };
    }

    public AuditDataIntegrityPayload CheckDataIntegrity(DataIntegrityCheckRequest request)
    {
        var maxIssues = Math.Clamp(request.MaxIssues, 1, 1000);
        var trails = _repository.SnapshotTrails();
        var reports = _repository.SnapshotReports();

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

    private static void UpdateAggregate(Dictionary<string, AggregateStats> bucket, string key, AuditTrailRecord record)
    {
        if (!bucket.TryGetValue(key, out var stats))
        {
            stats = new AggregateStats();
            bucket[key] = stats;
        }

        stats.Count++;
        stats.TotalDuration += record.DurationMs;

        if (record.StatusCode >= 400)
        {
            stats.Failures++;
        }

        if (record.IsSensitiveOperation)
        {
            stats.Sensitive++;
        }
    }

    private static List<string> BuildOptimizationSuggestions(
        int totalRecords,
        int failedRecords,
        int slowRecords,
        int sensitiveOperations,
        List<AuditBehaviorPathSummary> topPaths)
    {
        var suggestions = new List<string>();
        if (totalRecords == 0)
        {
            suggestions.Add("目前時窗內沒有稽核資料，建議先確認 audit trail 留存設定與流量來源。");
            return suggestions;
        }

        var failureRate = failedRecords / (double)totalRecords;
        if (failureRate >= 0.2)
        {
            suggestions.Add($"失敗率偏高（{failureRate:P0}），建議優先排查高失敗端點並建立重試/回退策略。");
        }

        var slowRate = slowRecords / (double)totalRecords;
        if (slowRate >= 0.15)
        {
            var hotspot = topPaths.OrderByDescending(x => x.AvgDurationMs).FirstOrDefault()?.PathKey;
            suggestions.Add($"慢請求比例偏高（{slowRate:P0}），建議優先分析端點：{hotspot ?? "(無)"}，搭配快取/索引優化。");
        }

        var sensitiveRate = sensitiveOperations / (double)totalRecords;
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

    private static AuditTrailTraceVisualization BuildTraceVisualization(List<AuditTrailTraceStep> steps)
    {
        var visualization = new AuditTrailTraceVisualization();
        if (steps.Count == 0)
        {
            return visualization;
        }

        var nodeHits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var edgeHits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < steps.Count; i++)
        {
            var nodeId = BuildTraceNodeId(steps[i]);
            nodeHits[nodeId] = nodeHits.TryGetValue(nodeId, out var hit) ? hit + 1 : 1;

            if (i == 0)
            {
                continue;
            }

            var prevNode = BuildTraceNodeId(steps[i - 1]);
            var edgeKey = $"{prevNode}->{nodeId}";
            edgeHits[edgeKey] = edgeHits.TryGetValue(edgeKey, out var edgeCount) ? edgeCount + 1 : 1;
        }

        visualization.Nodes = nodeHits
            .Select(x => new AuditTraceNode { NodeId = x.Key, Label = x.Key, HitCount = x.Value })
            .ToList();

        visualization.Edges = edgeHits
            .Select(x =>
            {
                var parts = x.Key.Split("->", 2);
                return new AuditTraceEdge
                {
                    FromNodeId = parts[0],
                    ToNodeId = parts[1],
                    TransitionCount = x.Value
                };
            })
            .ToList();

        var builder = new StringBuilder("flowchart LR\n");
        foreach (var edge in visualization.Edges)
        {
            builder.AppendLine($"    \"{edge.FromNodeId}\" -->|{edge.TransitionCount}| \"{edge.ToNodeId}\"");
        }

        visualization.MermaidFlowchart = builder.ToString().TrimEnd();
        return visualization;
    }

    private static List<string> BuildTraceExplainabilityNotes(List<AuditTrailTraceStep> steps)
    {
        var notes = new List<string>();
        if (steps.Count == 0)
        {
            notes.Add("查無符合條件的 trace steps。");
            return notes;
        }

        var failures = steps.Count(x => x.StatusCode >= 400);
        var highRisk = steps.Count(x => string.Equals(x.RiskLevel, "high", StringComparison.OrdinalIgnoreCase));
        var avgDuration = (long)Math.Round(steps.Average(x => x.DurationMs));

        notes.Add($"共 {steps.Count} 個步驟，失敗步驟 {failures} 個，高風險步驟 {highRisk} 個。");
        notes.Add($"平均耗時 {avgDuration}ms，首步驟時間 {steps.First().TimestampUtc:O}，末步驟時間 {steps.Last().TimestampUtc:O}。");

        return notes;
    }

    private static string BuildTraceNodeId(AuditTrailTraceStep step)
        => $"{step.Method} {step.Path}";

    private static bool IsSortedByTimestamp(List<AuditTrailTraceStep> steps)
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

    private sealed class AggregateStats
    {
        public int Count { get; set; }
        public int Failures { get; set; }
        public int Sensitive { get; set; }
        public long TotalDuration { get; set; }
    }
}
