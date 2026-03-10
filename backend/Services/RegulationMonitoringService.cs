using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IRegulationMonitoringService
{
    RegulationDocumentSnapshot UpsertSnapshot(RegulationSnapshotUpsertRequest request);
    Task<RegulationImpactAnalysisRecord> AnalyzeLatestAsync(RegulationImpactAnalysisRequest request, CancellationToken cancellationToken);
    RegulationImpactQueryPayload QueryImpactReports(RegulationImpactQueryRequest request);
}

public class RegulationMonitoringService : IRegulationMonitoringService
{
    private readonly ConcurrentQueue<RegulationDocumentSnapshot> _snapshots = new();
    private readonly ConcurrentQueue<RegulationImpactAnalysisRecord> _impactReports = new();

    public RegulationDocumentSnapshot UpsertSnapshot(RegulationSnapshotUpsertRequest request)
    {
        var source = request.Source.Trim();
        var documentCode = request.DocumentCode.Trim();
        var title = request.Title.Trim();
        var content = request.Content.Trim();

        var snapshot = new RegulationDocumentSnapshot
        {
            SnapshotId = $"reg-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32],
            Source = source,
            DocumentCode = documentCode,
            Title = title,
            Content = content,
            PublishedAtUtc = request.PublishedAtUtc?.ToUniversalTime() ?? DateTime.UtcNow,
            CapturedAtUtc = DateTime.UtcNow,
            Url = request.Url?.Trim(),
            Clauses = ExtractClauses(content)
        };

        _snapshots.Enqueue(snapshot);
        while (_snapshots.Count > 3000 && _snapshots.TryDequeue(out _))
        {
        }

        return snapshot;
    }

    public Task<RegulationImpactAnalysisRecord> AnalyzeLatestAsync(RegulationImpactAnalysisRequest request, CancellationToken cancellationToken)
    {
        var source = request.Source.Trim();
        var documentCode = request.DocumentCode.Trim();

        var pair = _snapshots
            .Where(x => string.Equals(x.Source, source, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(x.DocumentCode, documentCode, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.PublishedAtUtc)
            .Take(2)
            .ToList();

        if (pair.Count < 2)
        {
            throw new InvalidOperationException("至少需有兩版法規快照才能進行影響分析");
        }

        var current = pair[0];
        var baseline = pair[1];

        var changes = CompareClauses(baseline.Clauses, current.Clauses);
        var impactAreas = AnalyzeImpact(changes);

        var report = new RegulationImpactAnalysisRecord
        {
            AnalysisId = $"impact-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32],
            GeneratedAtUtc = DateTime.UtcNow,
            Source = current.Source,
            DocumentCode = current.DocumentCode,
            Title = current.Title,
            BaselinePublishedAtUtc = baseline.PublishedAtUtc,
            CurrentPublishedAtUtc = current.PublishedAtUtc,
            Changes = changes,
            ImpactAreas = impactAreas,
            RecommendedActions = BuildRecommendations(impactAreas)
        };

        _impactReports.Enqueue(report);
        while (_impactReports.Count > 2000 && _impactReports.TryDequeue(out _))
        {
        }

        return Task.FromResult(report);
    }

    public RegulationImpactQueryPayload QueryImpactReports(RegulationImpactQueryRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _impactReports.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            var source = request.Source.Trim();
            query = query.Where(x => string.Equals(x.Source, source, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.DocumentCode))
        {
            var documentCode = request.DocumentCode.Trim();
            query = query.Where(x => string.Equals(x.DocumentCode, documentCode, StringComparison.OrdinalIgnoreCase));
        }

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
        return new RegulationImpactQueryPayload
        {
            Total = ordered.Count,
            Page = page,
            PageSize = pageSize,
            Records = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList()
        };
    }

    private static List<RegulationChangeItem> CompareClauses(List<string> baselineClauses, List<string> currentClauses)
    {
        var oldDict = ToClauseDictionary(baselineClauses);
        var newDict = ToClauseDictionary(currentClauses);
        var allKeys = oldDict.Keys.Union(newDict.Keys).Distinct(StringComparer.OrdinalIgnoreCase);

        var changes = new List<RegulationChangeItem>();
        foreach (var key in allKeys)
        {
            oldDict.TryGetValue(key, out var previousText);
            newDict.TryGetValue(key, out var currentText);

            if (previousText is null && currentText is not null)
            {
                changes.Add(new RegulationChangeItem { ChangeType = "added", ClauseKey = key, CurrentText = currentText });
                continue;
            }

            if (previousText is not null && currentText is null)
            {
                changes.Add(new RegulationChangeItem { ChangeType = "removed", ClauseKey = key, PreviousText = previousText });
                continue;
            }

            if (!string.Equals(previousText, currentText, StringComparison.Ordinal))
            {
                changes.Add(new RegulationChangeItem
                {
                    ChangeType = "updated",
                    ClauseKey = key,
                    PreviousText = previousText,
                    CurrentText = currentText
                });
            }
        }

        return changes.OrderBy(x => x.ClauseKey, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static Dictionary<string, string> ToClauseDictionary(List<string> clauses)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < clauses.Count; i++)
        {
            var clause = clauses[i];
            var key = ExtractClauseKey(clause, i);
            dict[key] = clause;
        }

        return dict;
    }

    private static string ExtractClauseKey(string clause, int fallbackIndex)
    {
        var match = Regex.Match(clause, @"^\s*([第\d一二三四五六七八九十百千條項款\(\)\-\.]{1,16})");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return $"clause-{fallbackIndex + 1:D3}";
    }

    private static List<string> ExtractClauses(string content)
    {
        var normalized = content.Replace("\r", "\n");
        var lines = normalized
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(x => x.Split('。', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(x => x.Trim())
            .Where(x => x.Length >= 3)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return lines.Count > 0 ? lines : new List<string> { content };
    }

    private static List<RegulationImpactArea> AnalyzeImpact(List<RegulationChangeItem> changes)
    {
        var impacts = new Dictionary<string, RegulationImpactArea>(StringComparer.OrdinalIgnoreCase);

        foreach (var change in changes)
        {
            var text = $"{change.PreviousText} {change.CurrentText}";
            if (ContainsAny(text, "申報", "報送", "時限", "期限", "頻率"))
            {
                UpsertImpact(impacts, "申報流程", change.ChangeType == "added" ? "high" : "medium", "申報時點或流程條件可能需要調整");
            }

            if (ContainsAny(text, "欄位", "格式", "附表", "模板", "代碼"))
            {
                UpsertImpact(impacts, "報表格式", "medium", "輸出欄位或格式規範可能有變動");
            }

            if (ContainsAny(text, "客戶", "交易", "KYC", "AML", "蒐集", "資料"))
            {
                UpsertImpact(impacts, "數據採集", "high", "資料蒐集範圍或驗證規則可能需擴充");
            }

            if (ContainsAny(text, "保存", "留存", "年", "稽核", "軌跡"))
            {
                UpsertImpact(impacts, "稽核留痕", "medium", "稽核留痕與保存期間可能需調整");
            }
        }

        if (impacts.Count == 0 && changes.Count > 0)
        {
            impacts["作業流程"] = new RegulationImpactArea
            {
                Domain = "作業流程",
                Severity = "low",
                Reason = "有法規條文異動，建議進行人工複核"
            };
        }

        return impacts.Values.OrderByDescending(x => SeverityScore(x.Severity)).ToList();
    }

    private static List<string> BuildRecommendations(List<RegulationImpactArea> impacts)
    {
        var items = new List<string>();
        foreach (var impact in impacts)
        {
            switch (impact.Domain)
            {
                case "申報流程":
                    items.Add("檢查排程與截止日規則，必要時更新 /api/declare 相關驗證條件");
                    break;
                case "報表格式":
                    items.Add("盤點報表欄位映射與輸出模板，補上回歸測試案例");
                    break;
                case "數據採集":
                    items.Add("更新資料蒐集欄位與驗證規則，並同步修訂資料字典");
                    break;
                case "稽核留痕":
                    items.Add("確認稽核紀錄欄位完整性與保存策略是否符合新規");
                    break;
                default:
                    items.Add("安排合規與業務團隊進行人工差異審閱");
                    break;
            }
        }

        return items.Distinct(StringComparer.Ordinal).ToList();
    }

    private static bool ContainsAny(string text, params string[] keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static void UpsertImpact(Dictionary<string, RegulationImpactArea> impacts, string domain, string severity, string reason)
    {
        if (!impacts.TryGetValue(domain, out var current))
        {
            impacts[domain] = new RegulationImpactArea
            {
                Domain = domain,
                Severity = severity,
                Reason = reason
            };
            return;
        }

        if (SeverityScore(severity) > SeverityScore(current.Severity))
        {
            current.Severity = severity;
        }
    }

    private static int SeverityScore(string severity)
        => severity.ToLowerInvariant() switch
        {
            "high" => 3,
            "medium" => 2,
            _ => 1
        };
}
