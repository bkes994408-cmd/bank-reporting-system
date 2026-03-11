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
        var windowOverride = request.WindowMinutes.HasValue
            ? Math.Clamp(request.WindowMinutes.Value, 1, 24 * 60)
            : (int?)null;
        var topSubjects = request.TopSubjects.HasValue ? Math.Clamp(request.TopSubjects.Value, 1, 10) : 3;

        var enabledRules = _rules.Values.Where(x => x.Enabled).ToList();
        var uniqueWindows = enabledRules
            .Select(rule => windowOverride ?? rule.WindowMinutes)
            .Distinct()
            .ToList();

        var windowData = uniqueWindows.ToDictionary(
            w => w,
            w => QueryAllTrails(evaluatedAt.AddMinutes(-w), evaluatedAt));

        var generated = new List<ComplianceAlertRecord>();
        foreach (var rule in enabledRules)
        {
            var effectiveWindow = windowOverride ?? rule.WindowMinutes;
            if (!windowData.TryGetValue(effectiveWindow, out var records))
            {
                continue;
            }

            var alert = EvaluateRule(rule, records, effectiveWindow, topSubjects, request.NotifyChannels ?? new List<string>());
            if (alert is null)
            {
                continue;
            }

            _alerts.Enqueue(alert);
            generated.Add(alert);
        }

        while (_alerts.Count > 5000 && _alerts.TryDequeue(out _))
        {
        }

        return new ComplianceAlertEvaluateResult
        {
            EvaluatedAtUtc = evaluatedAt,
            EvaluatedRules = enabledRules.Count,
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

    private List<AuditTrailRecord> QueryAllTrails(DateTime startDateUtc, DateTime endDateUtc)
    {
        const int pageSize = 500;
        var page = 1;
        var records = new List<AuditTrailRecord>();

        while (true)
        {
            var payload = _complianceAuditService.QueryTrails(new AuditTrailQueryRequest
            {
                StartDateUtc = startDateUtc,
                EndDateUtc = endDateUtc,
                Page = page,
                PageSize = pageSize
            });

            if (payload.Records.Count == 0)
            {
                break;
            }

            records.AddRange(payload.Records);
            if (records.Count >= payload.Total || payload.Records.Count < pageSize)
            {
                break;
            }

            page++;
        }

        return records;
    }

    private ComplianceAlertRecord? EvaluateRule(
        ComplianceAlertRule rule,
        List<AuditTrailRecord> records,
        int effectiveWindowMinutes,
        int topSubjects,
        List<string> notifyChannels)
    {
        var candidateRecords = rule.SensitiveOnly
            ? records.Where(x => x.IsSensitiveOperation).ToList()
            : records;

        IEnumerable<IGrouping<string, AuditTrailRecord>> grouped = Enumerable.Empty<IGrouping<string, AuditTrailRecord>>();

        switch (rule.RuleType)
        {
            case "failed_requests":
                grouped = candidateRecords
                    .Where(x => x.StatusCode >= 400)
                    .GroupBy(x => x.User, StringComparer.OrdinalIgnoreCase);
                break;
            case "high_risk_operations":
                grouped = candidateRecords
                    .Where(x => string.Equals(x.RiskLevel, rule.RiskLevel ?? "high", StringComparison.OrdinalIgnoreCase))
                    .GroupBy(x => x.User, StringComparer.OrdinalIgnoreCase);
                break;
            case "off_hours_sensitive":
                grouped = candidateRecords
                    .Where(x => x.IsSensitiveOperation && (x.TimestampUtc.Hour < 6 || x.TimestampUtc.Hour >= 22))
                    .GroupBy(x => x.User, StringComparer.OrdinalIgnoreCase);
                break;
            default:
                return null;
        }

        var triggeredSubjects = grouped
            .Select(g => new ComplianceAlertSubjectTrigger
            {
                Subject = g.Key,
                TriggerCount = g.Count(),
                TopPaths = g
                    .GroupBy(x => x.Path)
                    .OrderByDescending(pathGroup => pathGroup.Count())
                    .Take(3)
                    .Select(pathGroup => $"{pathGroup.Key}({pathGroup.Count()})")
                    .ToList()
            })
            .Where(x => x.TriggerCount >= rule.Threshold)
            .OrderByDescending(x => x.TriggerCount)
            .ThenBy(x => x.Subject, StringComparer.OrdinalIgnoreCase)
            .Take(topSubjects)
            .ToList();

        if (triggeredSubjects.Count == 0)
        {
            return null;
        }

        var primary = triggeredSubjects[0];

        return new ComplianceAlertRecord
        {
            AlertId = $"alert-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32],
            RuleId = rule.RuleId,
            RuleName = rule.Name,
            Severity = rule.Severity,
            SeverityScore = SeverityScore(rule.Severity),
            TriggeredAtUtc = DateTime.UtcNow,
            WindowMinutes = effectiveWindowMinutes,
            TriggerCount = primary.TriggerCount,
            Subject = primary.Subject,
            TopSubjects = triggeredSubjects,
            SuggestedAction = BuildSuggestedAction(rule, primary.TriggerCount),
            TriggerDetails = new List<string>
            {
                $"primaryUser={primary.Subject}",
                $"primaryCount={primary.TriggerCount}",
                $"triggeredSubjects={triggeredSubjects.Count}",
                $"windowMinutes={effectiveWindowMinutes}",
                $"sensitiveOnly={rule.SensitiveOnly}",
                $"topSubjects={string.Join(";", triggeredSubjects.Select(x => $"{x.Subject}:{x.TriggerCount}"))}"
            },
            NotifyChannels = notifyChannels.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

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
            SensitiveOnly = false
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
            SensitiveOnly = false
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
            SensitiveOnly = true
        });
    }
}
