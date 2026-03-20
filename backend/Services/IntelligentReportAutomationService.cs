using System.Collections.Concurrent;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IIntelligentReportAutomationService
{
    Task<IntelligentReportSubmissionRecord> AutoGenerateAndSubmitAsync(IntelligentReportAutoSubmitRequest request, CancellationToken cancellationToken);
    IntelligentReportSubmissionQueryPayload Query(IntelligentReportSubmissionQueryRequest request);
}

public class IntelligentReportAutomationService : IIntelligentReportAutomationService
{
    private static readonly HashSet<string> SupportedReportIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "AI302", // 資產負債表
        "AI501", // 存放款利率表
        "AI812"  // 資本適足率報表
    };

    private readonly IAgentService _agentService;
    private readonly IPredictiveComplianceRiskService? _predictiveComplianceRiskService;
    private readonly ConcurrentQueue<IntelligentReportSubmissionRecord> _records = new();

    public IntelligentReportAutomationService(
        IAgentService agentService,
        IPredictiveComplianceRiskService? predictiveComplianceRiskService = null)
    {
        _agentService = agentService;
        _predictiveComplianceRiskService = predictiveComplianceRiskService;
    }

    public async Task<IntelligentReportSubmissionRecord> AutoGenerateAndSubmitAsync(IntelligentReportAutoSubmitRequest request, CancellationToken cancellationToken)
    {
        if (!SupportedReportIds.Contains(request.ReportId))
        {
            throw new InvalidOperationException($"目前僅支援標準化報表：{string.Join(", ", SupportedReportIds)}");
        }

        var generatedAt = DateTime.UtcNow;
        var standardizedReport = GenerateStandardizedReport(request.ReportId, request.SourceData, generatedAt);
        var warnings = BuildWarnings(request.ReportId, request.SourceData);
        var requestId = BuildRequestId(request.BankCode, request.ReportYear, request.ReportMonth, request.ReportId, generatedAt);

        var record = new IntelligentReportSubmissionRecord
        {
            AutomationId = $"auto-{generatedAt:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..36],
            GeneratedAtUtc = generatedAt,
            BankCode = request.BankCode,
            BankName = request.BankName,
            ReportYear = request.ReportYear,
            ReportMonth = request.ReportMonth,
            ReportId = request.ReportId,
            DryRun = request.DryRun,
            StandardizedReport = standardizedReport,
            ValidationWarnings = warnings,
            RequestId = requestId,
            Status = request.DryRun ? "dry-run" : "generated"
        };

        AttachPredictiveRiskSnapshot(request, record);

        if (!request.DryRun)
        {
            var declareResponse = await _agentService.DeclareAsync(new DeclareRequest
            {
                RequestId = requestId,
                BankCode = request.BankCode,
                BankName = request.BankName,
                ReportYear = request.ReportYear,
                ReportMonth = request.ReportMonth,
                ReportId = request.ReportId,
                ContractorName = request.ContractorName,
                ContractorTel = request.ContractorTel,
                ContractorEmail = request.ContractorEmail,
                ManagerName = request.ManagerName,
                ManagerTel = request.ManagerTel,
                ManagerEmail = request.ManagerEmail,
                Report = standardizedReport
            });

            record.SubmissionCode = declareResponse.Code;
            record.SubmissionMessage = declareResponse.Msg;
            record.Status = declareResponse.Code == "0000" ? "submitted" : "failed";
        }

        _records.Enqueue(record);
        while (_records.Count > 2000 && _records.TryDequeue(out _))
        {
        }

        return record;
    }

    public IntelligentReportSubmissionQueryPayload Query(IntelligentReportSubmissionQueryRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _records.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.ReportId))
        {
            var reportId = request.ReportId.Trim();
            query = query.Where(x => string.Equals(x.ReportId, reportId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (request.FromGeneratedAtUtc.HasValue)
        {
            var from = request.FromGeneratedAtUtc.Value.ToUniversalTime();
            query = query.Where(x => x.GeneratedAtUtc >= from);
        }

        if (request.ToGeneratedAtUtc.HasValue)
        {
            var to = request.ToGeneratedAtUtc.Value.ToUniversalTime();
            query = query.Where(x => x.GeneratedAtUtc <= to);
        }

        var ordered = query.OrderByDescending(x => x.GeneratedAtUtc).ToList();

        return new IntelligentReportSubmissionQueryPayload
        {
            Total = ordered.Count,
            Page = page,
            PageSize = pageSize,
            Records = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList()
        };
    }

    private void AttachPredictiveRiskSnapshot(IntelligentReportAutoSubmitRequest request, IntelligentReportSubmissionRecord record)
    {
        if (!request.EnablePredictiveRiskAssessment || _predictiveComplianceRiskService is null)
        {
            return;
        }

        var assessment = _predictiveComplianceRiskService.Assess(new PredictiveComplianceRiskAssessRequest
        {
            LookbackDays = request.PredictiveLookbackDays,
            ForecastDays = request.PredictiveForecastDays,
            FocusAreas = request.PredictiveFocusAreas
        });

        record.PredictiveRisk = new IntelligentReportRiskAssessmentSnapshot
        {
            AssessmentId = assessment.AssessmentId,
            PredictedRiskLevel = assessment.PredictedRiskLevel,
            RiskScore = assessment.RiskScore,
            ConfidenceScore = assessment.ConfidenceScore,
            TrendDirection = assessment.TrendForecast.Direction,
            EarlyWarnings = assessment.EarlyWarnings.Take(3).ToList(),
            RecommendedActions = assessment.RecommendedActions.Take(3).ToList()
        };

        if (assessment.PredictedRiskLevel is "high" or "critical")
        {
            record.ValidationWarnings.Add($"預測風險等級={assessment.PredictedRiskLevel}（score={assessment.RiskScore}），建議提交前由合規主管覆核。");
        }

        if (assessment.TrendForecast.Direction is "rising" or "rising_fast")
        {
            record.ValidationWarnings.Add($"預測風險走勢上升（slope/day={assessment.TrendForecast.SlopePerDay:F2}），建議在報告備註納入風險趨勢說明。");
        }
    }

    private static object GenerateStandardizedReport(string reportId, Dictionary<string, object>? sourceData, DateTime generatedAtUtc)
    {
        sourceData ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        return reportId.ToUpperInvariant() switch
        {
            "AI302" => new
            {
                reportId,
                generatedAtUtc,
                assets = GetDecimal(sourceData, "assets", 125000000m),
                liabilities = GetDecimal(sourceData, "liabilities", 98000000m),
                equity = GetDecimal(sourceData, "equity", 27000000m),
                currency = GetString(sourceData, "currency", "TWD")
            },
            "AI501" => new
            {
                reportId,
                generatedAtUtc,
                depositRate = GetDecimal(sourceData, "depositRate", 1.25m),
                loanRate = GetDecimal(sourceData, "loanRate", 2.85m),
                spread = GetDecimal(sourceData, "spread", 1.60m),
                benchmark = GetString(sourceData, "benchmark", "TAIBOR")
            },
            _ => new
            {
                reportId,
                generatedAtUtc,
                tier1CapitalRatio = GetDecimal(sourceData, "tier1CapitalRatio", 11.2m),
                totalCapitalRatio = GetDecimal(sourceData, "totalCapitalRatio", 14.8m),
                leverageRatio = GetDecimal(sourceData, "leverageRatio", 6.4m)
            }
        };
    }

    private static List<string> BuildWarnings(string reportId, Dictionary<string, object>? sourceData)
    {
        sourceData ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        if (!sourceData.Any())
        {
            warnings.Add("未提供 sourceData，系統已使用標準模板預設值生成報表。");
        }

        if (string.Equals(reportId, "AI812", StringComparison.OrdinalIgnoreCase))
        {
            var ratio = GetDecimal(sourceData, "totalCapitalRatio", 14.8m);
            if (ratio < 10.5m)
            {
                warnings.Add("總資本適足率偏低，建議提交前進行人工覆核。");
            }
        }

        return warnings;
    }

    private static string BuildRequestId(string bankCode, string year, string month, string reportId, DateTime generatedAtUtc)
        => $"{bankCode}-{year}{month}-{reportId}-{generatedAtUtc:yyyyMMddHHmmss}";

    private static decimal GetDecimal(IDictionary<string, object> source, string key, decimal fallback)
    {
        if (!source.TryGetValue(key, out var value) || value is null)
        {
            return fallback;
        }

        return value switch
        {
            decimal d => d,
            int i => i,
            long l => l,
            double db => (decimal)db,
            float f => (decimal)f,
            string s when decimal.TryParse(s, out var parsed) => parsed,
            _ => fallback
        };
    }

    private static string GetString(IDictionary<string, object> source, string key, string fallback)
    {
        if (!source.TryGetValue(key, out var value) || value is null)
        {
            return fallback;
        }

        var text = value.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }
}
