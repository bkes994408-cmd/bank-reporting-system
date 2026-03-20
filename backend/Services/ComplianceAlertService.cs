using System.Collections.Concurrent;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IComplianceAlertService
{
    ComplianceAlertRule UpsertRule(ComplianceAlertRuleUpsertRequest request);
    ComplianceAlertRulesPayload QueryRules(ComplianceAlertRulesQueryRequest request);
    ComplianceAlertEvaluateResult Evaluate(ComplianceAlertEvaluateRequest request);
    ComplianceAlertQueryPayload QueryAlerts(ComplianceAlertQueryRequest request);
}

public class ComplianceAlertService : IComplianceAlertService
{
    private readonly IComplianceAuditService _complianceAuditService;
    private readonly ConcurrentDictionary<string, ComplianceAlertRule> _rules = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<ComplianceAlertRecord> _alerts = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastTriggeredByRuleAndSubject = new(StringComparer.OrdinalIgnoreCase);

    public ComplianceAlertService(IComplianceAuditService complianceAuditService)
    {
        _complianceAuditService = complianceAuditService;
        SeedDefaultRules();
    }

    public ComplianceAlertRule UpsertRule(ComplianceAlertRuleUpsertRequest request)
    {
        var now = DateTime.UtcNow;
        var ruleId = string.IsNullOrWhiteSpace(request.RuleId)
            ? $"rule-{Guid.NewGuid():N}"[..24]
            : request.RuleId.Trim();

        var rule = new ComplianceAlertRule
        {
            RuleId = ruleId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? "未命名規則" : request.Name.Trim(),
            RuleType = NormalizeRuleType(request.RuleType),
            Enabled = request.Enabled,
            Severity = NormalizeSeverity(request.Severity),
            Threshold = Math.Max(1, request.Threshold),
            WindowMinutes = Math.Clamp(request.WindowMinutes, 1, 24 * 60),
            RiskLevel = string.IsNullOrWhiteSpace(request.RiskLevel) ? null : request.RiskLevel.Trim().ToLowerInvariant(),
            SensitiveOnly = request.SensitiveOnly,
            MinErrorRatePercent = Math.Clamp(request.MinErrorRatePercent, 0, 100),
            MinDistinctPaths = Math.Max(1, request.MinDistinctPaths),
            CooldownMinutes = Math.Clamp(request.CooldownMinutes, 0, 24 * 60),
            ExcludedPaths = (request.ExcludedPaths ?? [])
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            UpdatedAtUtc = now
        };

        _rules[rule.RuleId] = rule;
        return rule;
    }

    public ComplianceAlertRulesPayload QueryRules(ComplianceAlertRulesQueryRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _rules.Values.AsEnumerable();
        if (request.Enabled.HasValue)
        {
            query = query.Where(x => x.Enabled == request.Enabled.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.RuleType))
        {
            var ruleType = request.RuleType.Trim();
            query = query.Where(x => string.Equals(x.RuleType, ruleType, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = query.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();

        return new ComplianceAlertRulesPayload
        {
            Total = ordered.Count,
            Page = page,
            PageSize = pageSize,
            Rules = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList()
        };
    }

    public ComplianceAlertEvaluateResult Evaluate(ComplianceAlertEvaluateRequest request)
    {
        var evaluatedAt = DateTime.UtcNow;
        var uniqueWindows = _rules.Values.Where(x => x.Enabled).Select(x => x.WindowMinutes).Distinct().ToList();
        if (request.WindowMinutes.HasValue)
        {
            uniqueWindows.Add(Math.Clamp(request.WindowMinutes.Value, 1, 24 * 60));
        }

        uniqueWindows = uniqueWindows.Distinct().ToList();
        var windowData = uniqueWindows.ToDictionary(
            w => w,
            w => _complianceAuditService.QueryTrails(new AuditTrailQueryRequest
            {
                StartDateUtc = evaluatedAt.AddMinutes(-w),
                EndDateUtc = evaluatedAt,
                Page = 1,
                PageSize = 500
            }).Records);

        var generated = new List<ComplianceAlertRecord>();
        foreach (var rule in _rules.Values.Where(x => x.Enabled))
        {
            if (!windowData.TryGetValue(rule.WindowMinutes, out var records))
            {
                continue;
            }

            var alert = EvaluateRule(rule, records, request.NotifyChannels ?? new List<string>());
            if (alert is null)
            {
                continue;
            }

            if (IsInCooldown(rule, alert.Subject, alert.TriggeredAtUtc))
            {
                continue;
            }

            _alerts.Enqueue(alert);
            _lastTriggeredByRuleAndSubject[BuildCooldownKey(rule.RuleId, alert.Subject)] = alert.TriggeredAtUtc;
            generated.Add(alert);
        }

        while (_alerts.Count > 5000 && _alerts.TryDequeue(out _))
        {
        }

        return new ComplianceAlertEvaluateResult
        {
            EvaluatedAtUtc = evaluatedAt,
            EvaluatedRules = _rules.Values.Count(x => x.Enabled),
            TriggeredAlerts = generated.Count,
            Alerts = generated.OrderByDescending(x => x.SeverityScore).ThenByDescending(x => x.TriggerCount).ToList()
        };
    }

    public ComplianceAlertQueryPayload QueryAlerts(ComplianceAlertQueryRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _alerts.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            var severity = request.Severity.Trim();
            query = query.Where(x => string.Equals(x.Severity, severity, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.RuleId))
        {
            var ruleId = request.RuleId.Trim();
            query = query.Where(x => string.Equals(x.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));
        }

        if (request.FromTriggeredAtUtc.HasValue)
        {
            var fromUtc = request.FromTriggeredAtUtc.Value.ToUniversalTime();
            query = query.Where(x => x.TriggeredAtUtc >= fromUtc);
        }

        if (request.ToTriggeredAtUtc.HasValue)
        {
            var toUtc = request.ToTriggeredAtUtc.Value.ToUniversalTime();
            query = query.Where(x => x.TriggeredAtUtc <= toUtc);
        }

        var ordered = query.OrderByDescending(x => x.TriggeredAtUtc).ToList();
        return new ComplianceAlertQueryPayload
        {
            Total = ordered.Count,
            Page = page,
            PageSize = pageSize,
            Alerts = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList()
        };
    }

    private ComplianceAlertRecord? EvaluateRule(ComplianceAlertRule rule, List<AuditTrailRecord> records, List<string> notifyChannels)
    {
        var candidateRecords = records
            .Where(x => !IsPathExcluded(rule, x.Path))
            .ToList();

        if (rule.SensitiveOnly)
        {
            candidateRecords = candidateRecords.Where(x => x.IsSensitiveOperation).ToList();
        }

        if (candidateRecords.Count == 0)
        {
            return null;
        }

        switch (rule.RuleType)
        {
            case "failed_requests":
                return EvaluateFailedRequestsRule(rule, candidateRecords, notifyChannels);
            case "high_risk_operations":
                return EvaluateHighRiskRule(rule, candidateRecords, notifyChannels);
            case "off_hours_sensitive":
                return EvaluateOffHoursRule(rule, candidateRecords, notifyChannels);
            default:
                return null;
        }
    }

    private static ComplianceAlertRecord? EvaluateFailedRequestsRule(ComplianceAlertRule rule, List<AuditTrailRecord> records, List<string> notifyChannels)
    {
        var grouped = records
            .GroupBy(x => x.User, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var failed = g.Where(x => x.StatusCode >= 400).ToList();
                var total = g.Count();
                var distinctPaths = failed.Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                var errorRate = total == 0 ? 0 : failed.Count * 100.0 / total;
                return new
                {
                    User = g.Key,
                    Failed = failed,
                    Total = total,
                    DistinctPaths = distinctPaths,
                    ErrorRate = errorRate
                };
            })
            .Where(x => x.Failed.Count >= rule.Threshold)
            .Where(x => x.DistinctPaths >= rule.MinDistinctPaths)
            .Where(x => x.ErrorRate >= rule.MinErrorRatePercent)
            .OrderByDescending(x => x.Failed.Count)
            .ThenByDescending(x => x.ErrorRate)
            .FirstOrDefault();

        if (grouped is null)
        {
            return null;
        }

        var topPaths = grouped.Failed
            .GroupBy(x => x.Path)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => $"{g.Key}({g.Count()})")
            .ToList();

        var explainability = new ComplianceAlertExplainability
        {
            TriggerReasons =
            [
                $"失敗請求次數達門檻（{grouped.Failed.Count} >= {rule.Threshold}）",
                $"錯誤率達門檻（{grouped.ErrorRate:F1}% >= {rule.MinErrorRatePercent}%）",
                $"影響端點數達門檻（{grouped.DistinctPaths} >= {rule.MinDistinctPaths}）"
            ],
            Metrics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["failedCount"] = grouped.Failed.Count.ToString(),
                ["totalCount"] = grouped.Total.ToString(),
                ["errorRatePercent"] = grouped.ErrorRate.ToString("F1"),
                ["distinctPaths"] = grouped.DistinctPaths.ToString()
            },
            EvidenceSamples = grouped.Failed
                .OrderByDescending(x => x.TimestampUtc)
                .Take(3)
                .Select(x => $"{x.TimestampUtc:O} {x.Method} {x.Path} {x.StatusCode}")
                .ToList()
        };

        return BuildAlert(rule, grouped.User, grouped.Failed.Count, topPaths, notifyChannels, explainability);
    }

    private static ComplianceAlertRecord? EvaluateHighRiskRule(ComplianceAlertRule rule, List<AuditTrailRecord> records, List<string> notifyChannels)
    {
        var grouped = records
            .Where(x => string.Equals(x.RiskLevel, rule.RiskLevel ?? "high", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.User, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                User = g.Key,
                Records = g.ToList(),
                DistinctPaths = g.Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            })
            .Where(x => x.Records.Count >= rule.Threshold)
            .Where(x => x.DistinctPaths >= rule.MinDistinctPaths)
            .OrderByDescending(x => x.Records.Count)
            .FirstOrDefault();

        if (grouped is null)
        {
            return null;
        }

        var topPaths = grouped.Records
            .GroupBy(x => x.Path)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => $"{g.Key}({g.Count()})")
            .ToList();

        var explainability = new ComplianceAlertExplainability
        {
            TriggerReasons =
            [
                $"高風險操作次數達門檻（{grouped.Records.Count} >= {rule.Threshold}）",
                $"影響端點數達門檻（{grouped.DistinctPaths} >= {rule.MinDistinctPaths}）"
            ],
            Metrics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["highRiskCount"] = grouped.Records.Count.ToString(),
                ["distinctPaths"] = grouped.DistinctPaths.ToString()
            },
            EvidenceSamples = grouped.Records.OrderByDescending(x => x.TimestampUtc).Take(3)
                .Select(x => $"{x.TimestampUtc:O} {x.Method} {x.Path} risk={x.RiskLevel}")
                .ToList()
        };

        return BuildAlert(rule, grouped.User, grouped.Records.Count, topPaths, notifyChannels, explainability);
    }

    private static ComplianceAlertRecord? EvaluateOffHoursRule(ComplianceAlertRule rule, List<AuditTrailRecord> records, List<string> notifyChannels)
    {
        var grouped = records
            .Where(x => x.IsSensitiveOperation && (x.TimestampUtc.Hour < 6 || x.TimestampUtc.Hour >= 22))
            .GroupBy(x => x.User, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                User = g.Key,
                Records = g.ToList(),
                DistinctPaths = g.Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            })
            .Where(x => x.Records.Count >= rule.Threshold)
            .Where(x => x.DistinctPaths >= rule.MinDistinctPaths)
            .OrderByDescending(x => x.Records.Count)
            .FirstOrDefault();

        if (grouped is null)
        {
            return null;
        }

        var topPaths = grouped.Records
            .GroupBy(x => x.Path)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => $"{g.Key}({g.Count()})")
            .ToList();

        var explainability = new ComplianceAlertExplainability
        {
            TriggerReasons =
            [
                $"夜間敏感操作次數達門檻（{grouped.Records.Count} >= {rule.Threshold}）",
                $"影響端點數達門檻（{grouped.DistinctPaths} >= {rule.MinDistinctPaths}）"
            ],
            Metrics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["offHoursSensitiveCount"] = grouped.Records.Count.ToString(),
                ["distinctPaths"] = grouped.DistinctPaths.ToString()
            },
            EvidenceSamples = grouped.Records.OrderByDescending(x => x.TimestampUtc).Take(3)
                .Select(x => $"{x.TimestampUtc:O} {x.Method} {x.Path} sensitive={x.IsSensitiveOperation}")
                .ToList()
        };

        return BuildAlert(rule, grouped.User, grouped.Records.Count, topPaths, notifyChannels, explainability);
    }

    private static ComplianceAlertRecord BuildAlert(
        ComplianceAlertRule rule,
        string subject,
        int triggerCount,
        List<string> topPaths,
        List<string> notifyChannels,
        ComplianceAlertExplainability explainability)
    {
        return new ComplianceAlertRecord
        {
            AlertId = $"alert-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32],
            RuleId = rule.RuleId,
            RuleName = rule.Name,
            Severity = rule.Severity,
            SeverityScore = SeverityScore(rule.Severity),
            TriggeredAtUtc = DateTime.UtcNow,
            WindowMinutes = rule.WindowMinutes,
            TriggerCount = triggerCount,
            Subject = subject,
            SuggestedAction = BuildSuggestedAction(rule, triggerCount),
            TriggerDetails = new List<string>
            {
                $"user={subject}",
                $"count={triggerCount}",
                $"windowMinutes={rule.WindowMinutes}",
                $"topPaths={string.Join(",", topPaths)}"
            },
            NotifyChannels = notifyChannels.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Explainability = explainability
        };
    }

    private bool IsInCooldown(ComplianceAlertRule rule, string subject, DateTime nowUtc)
    {
        if (rule.CooldownMinutes <= 0)
        {
            return false;
        }

        if (!_lastTriggeredByRuleAndSubject.TryGetValue(BuildCooldownKey(rule.RuleId, subject), out var previousAt))
        {
            return false;
        }

        return previousAt.AddMinutes(rule.CooldownMinutes) > nowUtc;
    }

    private static string BuildCooldownKey(string ruleId, string subject)
        => $"{ruleId}|{subject}";

    private static bool IsPathExcluded(ComplianceAlertRule rule, string path)
        => rule.ExcludedPaths.Any(x => path.Contains(x, StringComparison.OrdinalIgnoreCase));

    private static string BuildSuggestedAction(ComplianceAlertRule rule, int triggerCount)
    {
        return rule.RuleType switch
        {
            "failed_requests" => triggerCount >= rule.Threshold * 2
                ? "暫時鎖定帳號並啟動人工調查"
                : "通知合規人員檢查登入與操作紀錄",
            "high_risk_operations" => "啟動高風險作業二次審核流程",
            "off_hours_sensitive" => "確認是否為授權夜間作業，必要時升級事件",
            _ => "通知合規人員人工覆核"
        };
    }

    private static int SeverityScore(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => 100,
        "high" => 80,
        "medium" => 60,
        _ => 40
    };

    private static string NormalizeRuleType(string ruleType)
    {
        var normalized = string.IsNullOrWhiteSpace(ruleType) ? "failed_requests" : ruleType.Trim().ToLowerInvariant();
        return normalized is "failed_requests" or "high_risk_operations" or "off_hours_sensitive"
            ? normalized
            : "failed_requests";
    }

    private static string NormalizeSeverity(string severity)
    {
        var normalized = string.IsNullOrWhiteSpace(severity) ? "medium" : severity.Trim().ToLowerInvariant();
        return normalized is "low" or "medium" or "high" or "critical" ? normalized : "medium";
    }

    private void SeedDefaultRules()
    {
        UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "failed-requests-burst",
            Name = "短時間失敗請求異常",
            RuleType = "failed_requests",
            Enabled = true,
            Severity = "high",
            Threshold = 5,
            WindowMinutes = 15,
            SensitiveOnly = false,
            MinErrorRatePercent = 50,
            MinDistinctPaths = 1,
            CooldownMinutes = 10,
            ExcludedPaths = ["/health", "/metrics"]
        });

        UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "high-risk-operations-burst",
            Name = "高風險操作集中發生",
            RuleType = "high_risk_operations",
            Enabled = true,
            Severity = "critical",
            Threshold = 3,
            WindowMinutes = 30,
            RiskLevel = "high",
            SensitiveOnly = false,
            MinDistinctPaths = 1,
            CooldownMinutes = 5
        });

        UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "off-hours-sensitive-operations",
            Name = "夜間敏感操作監控",
            RuleType = "off_hours_sensitive",
            Enabled = true,
            Severity = "medium",
            Threshold = 2,
            WindowMinutes = 120,
            SensitiveOnly = true,
            MinDistinctPaths = 1,
            CooldownMinutes = 15
        });
    }
}
