using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("api/compliance")]
public class ComplianceController : ControllerBase
{
    private readonly IComplianceAuditService _complianceAuditService;
    private readonly IRegulationMonitoringService _regulationMonitoringService;
    private readonly IExternalComplianceDataService _externalComplianceDataService;
    private readonly IComplianceAlertService _complianceAlertService;
    private readonly IFinancialMarketDataService _financialMarketDataService;
    private readonly IPredictiveComplianceRiskService _predictiveComplianceRiskService;
    private readonly IBlockchainComplianceService _blockchainComplianceService;
    private readonly IComplianceProofService _complianceProofService;

    public ComplianceController(
        IComplianceAuditService complianceAuditService,
        IRegulationMonitoringService regulationMonitoringService,
        IExternalComplianceDataService externalComplianceDataService,
        IComplianceAlertService complianceAlertService,
        IPredictiveComplianceRiskService predictiveComplianceRiskService,
        IBlockchainComplianceService blockchainComplianceService,
        IComplianceProofService complianceProofService,
        IFinancialMarketDataService? financialMarketDataService = null)
    {
        _complianceAuditService = complianceAuditService;
        _regulationMonitoringService = regulationMonitoringService;
        _externalComplianceDataService = externalComplianceDataService;
        _complianceAlertService = complianceAlertService;
        _financialMarketDataService = financialMarketDataService ?? new FinancialMarketDataService();
        _predictiveComplianceRiskService = predictiveComplianceRiskService;
        _blockchainComplianceService = blockchainComplianceService;
        _complianceProofService = complianceProofService;
    }

    [HttpPost("audit-reports/generate")]
    public async Task<IActionResult> GenerateAuditReport([FromBody] ComplianceAuditReportGenerateRequest request)
    {
        var sanitized = new ComplianceAuditReportGenerateRequest
        {
            StartDateUtc = request.StartDateUtc,
            EndDateUtc = request.EndDateUtc
        };

        var report = await _complianceAuditService.GenerateReportAsync(sanitized, CancellationToken.None);
        return Ok(new ApiResponse<ComplianceAuditReportRecord>
        {
            Code = "0000",
            Msg = "合規性審計報告生成成功",
            Payload = report
        });
    }

    [HttpPost("audit-reports/query")]
    public IActionResult QueryAuditReports([FromBody] ComplianceAuditReportQueryRequest request)
    {
        var sanitized = new ComplianceAuditReportQueryRequest
        {
            FromGeneratedAtUtc = request.FromGeneratedAtUtc,
            ToGeneratedAtUtc = request.ToGeneratedAtUtc,
            Page = request.Page,
            PageSize = request.PageSize
        };

        var result = _complianceAuditService.QueryReports(sanitized);
        return Ok(new ApiResponse<ComplianceAuditReportsPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = result
        });
    }

    [HttpPost("audit-trails/query")]
    public IActionResult QueryAuditTrails([FromBody] AuditTrailQueryRequest request)
    {
        var sanitized = new AuditTrailQueryRequest
        {
            User = request.User?.Trim(),
            Path = request.Path?.Trim(),
            RiskLevel = request.RiskLevel?.Trim(),
            SensitiveOnly = request.SensitiveOnly,
            MinStatusCode = request.MinStatusCode,
            MaxStatusCode = request.MaxStatusCode,
            MinDurationMs = request.MinDurationMs,
            StartDateUtc = request.StartDateUtc,
            EndDateUtc = request.EndDateUtc,
            Page = request.Page,
            PageSize = request.PageSize
        };

        var result = _complianceAuditService.QueryTrails(sanitized);
        return Ok(new ApiResponse<AuditTrailQueryPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = result
        });
    }

    [HttpPost("audit-trails/behavior-insights")]
    public IActionResult GetAuditBehaviorInsights([FromBody] AuditBehaviorInsightsRequest request)
    {
        var sanitized = new AuditBehaviorInsightsRequest
        {
            StartDateUtc = request.StartDateUtc,
            EndDateUtc = request.EndDateUtc,
            TopUsers = request.TopUsers,
            TopPaths = request.TopPaths
        };

        var result = _complianceAuditService.GetBehaviorInsights(sanitized);
        return Ok(new ApiResponse<AuditBehaviorInsightsPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = result
        });
    }

    [HttpPost("audit-trails/trace")]
    public IActionResult QueryAuditTrailTrace([FromBody] AuditTrailTraceRequest request)
    {
        var sanitized = new AuditTrailTraceRequest
        {
            TraceId = request.TraceId?.Trim(),
            User = request.User?.Trim(),
            StartDateUtc = request.StartDateUtc,
            EndDateUtc = request.EndDateUtc,
            MaxSteps = request.MaxSteps
        };

        var result = _complianceAuditService.QueryTrace(sanitized);
        return Ok(new ApiResponse<AuditTrailTracePayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = result
        });
    }

    [HttpPost("regulations/snapshots")]
    public IActionResult UpsertRegulationSnapshot([FromBody] RegulationSnapshotUpsertRequest request)
    {
        var sanitized = new RegulationSnapshotUpsertRequest
        {
            Source = request.Source?.Trim() ?? string.Empty,
            DocumentCode = request.DocumentCode?.Trim() ?? string.Empty,
            Title = request.Title?.Trim() ?? string.Empty,
            Content = request.Content ?? string.Empty,
            PublishedAtUtc = request.PublishedAtUtc,
            Url = request.Url?.Trim()
        };

        var snapshot = _regulationMonitoringService.UpsertSnapshot(sanitized);
        return Ok(new ApiResponse<RegulationDocumentSnapshot>
        {
            Code = "0000",
            Msg = "法規快照寫入成功",
            Payload = snapshot
        });
    }

    [HttpPost("regulations/impact-analysis/generate")]
    public async Task<IActionResult> GenerateRegulationImpactAnalysis([FromBody] RegulationImpactAnalysisRequest request)
    {
        var sanitized = new RegulationImpactAnalysisRequest
        {
            Source = request.Source?.Trim() ?? string.Empty,
            DocumentCode = request.DocumentCode?.Trim() ?? string.Empty
        };

        try
        {
            var report = await _regulationMonitoringService.AnalyzeLatestAsync(sanitized, CancellationToken.None);
            return Ok(new ApiResponse<RegulationImpactAnalysisRecord>
            {
                Code = "0000",
                Msg = "法規影響分析生成成功",
                Payload = report
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                Code = "COMPLIANCE_4001",
                Msg = ex.Message
            });
        }
    }

    [HttpPost("regulations/impact-analysis/query")]
    public IActionResult QueryRegulationImpactAnalysis([FromBody] RegulationImpactQueryRequest request)
    {
        var sanitized = new RegulationImpactQueryRequest
        {
            Source = request.Source?.Trim(),
            DocumentCode = request.DocumentCode?.Trim(),
            FromGeneratedAtUtc = request.FromGeneratedAtUtc,
            ToGeneratedAtUtc = request.ToGeneratedAtUtc,
            Page = request.Page,
            PageSize = request.PageSize
        };

        var result = _regulationMonitoringService.QueryImpactReports(sanitized);
        return Ok(new ApiResponse<RegulationImpactQueryPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = result
        });
    }

    [HttpPost("external-data/sync")]
    public async Task<IActionResult> SyncExternalRiskData([FromBody] ExternalRiskDataSyncRequest request)
    {
        var sanitized = new ExternalRiskDataSyncRequest
        {
            ProviderName = request.ProviderName?.Trim() ?? string.Empty,
            DatasetType = request.DatasetType?.Trim() ?? "sanctions",
            PathOverride = request.PathOverride?.Trim(),
            FieldMappings = ToCaseInsensitiveMappings(request.FieldMappings)
        };

        if (string.IsNullOrWhiteSpace(sanitized.ProviderName))
        {
            return BadRequest(new ApiResponse<object> { Code = "COMPLIANCE_4002", Msg = "providerName 為必填" });
        }

        try
        {
            var result = await _externalComplianceDataService.SyncRiskDataAsync(sanitized, CancellationToken.None);
            return Ok(new ApiResponse<ExternalRiskDataSyncResult>
            {
                Code = "0000",
                Msg = "外部風險數據同步成功",
                Payload = result
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                Code = "COMPLIANCE_4003",
                Msg = ex.Message
            });
        }
    }

    [HttpPost("external-data/screen")]
    public IActionResult ScreenExternalRisk([FromBody] ExternalRiskScreeningRequest request)
    {
        var sanitized = new ExternalRiskScreeningRequest
        {
            CustomerName = request.CustomerName?.Trim() ?? string.Empty,
            Country = request.Country?.Trim(),
            DatasetType = request.DatasetType?.Trim()
        };

        if (string.IsNullOrWhiteSpace(sanitized.CustomerName))
        {
            return BadRequest(new ApiResponse<object> { Code = "COMPLIANCE_4004", Msg = "customerName 為必填" });
        }

        var result = _externalComplianceDataService.ScreenRisk(sanitized);
        return Ok(new ApiResponse<ExternalRiskScreeningResult>
        {
            Code = "0000",
            Msg = "風險比對完成",
            Payload = result
        });
    }

    [HttpPost("alerts/rules/upsert")]
    public IActionResult UpsertAlertRule([FromBody] ComplianceAlertRuleUpsertRequest request)
    {
        var sanitized = new ComplianceAlertRuleUpsertRequest
        {
            RuleId = request.RuleId?.Trim(),
            Name = request.Name?.Trim() ?? string.Empty,
            RuleType = request.RuleType?.Trim() ?? "failed_requests",
            Enabled = request.Enabled,
            Severity = request.Severity?.Trim() ?? "medium",
            Threshold = request.Threshold,
            WindowMinutes = request.WindowMinutes,
            RiskLevel = request.RiskLevel?.Trim(),
            SensitiveOnly = request.SensitiveOnly
        };

        var rule = _complianceAlertService.UpsertRule(sanitized);
        return Ok(new ApiResponse<ComplianceAlertRule>
        {
            Code = "0000",
            Msg = "告警規則已更新",
            Payload = rule
        });
    }

    [HttpPost("alerts/rules/query")]
    public IActionResult QueryAlertRules([FromBody] ComplianceAlertRulesQueryRequest request)
    {
        var sanitized = new ComplianceAlertRulesQueryRequest
        {
            Enabled = request.Enabled,
            RuleType = request.RuleType?.Trim(),
            Page = request.Page,
            PageSize = request.PageSize
        };

        var result = _complianceAlertService.QueryRules(sanitized);
        return Ok(new ApiResponse<ComplianceAlertRulesPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = result
        });
    }

    [HttpPost("alerts/evaluate")]
    public IActionResult EvaluateAlerts([FromBody] ComplianceAlertEvaluateRequest request)
    {
        var sanitized = new ComplianceAlertEvaluateRequest
        {
            WindowMinutes = request.WindowMinutes,
            NotifyChannels = request.NotifyChannels
        };

        var result = _complianceAlertService.Evaluate(sanitized);
        return Ok(new ApiResponse<ComplianceAlertEvaluateResult>
        {
            Code = "0000",
            Msg = "告警評估完成",
            Payload = result
        });
    }

    [HttpPost("alerts/query")]
    public IActionResult QueryAlerts([FromBody] ComplianceAlertQueryRequest request)
    {
        var sanitized = new ComplianceAlertQueryRequest
        {
            RuleId = request.RuleId?.Trim(),
            Severity = request.Severity?.Trim(),
            FromTriggeredAtUtc = request.FromTriggeredAtUtc,
            ToTriggeredAtUtc = request.ToTriggeredAtUtc,
            Page = request.Page,
            PageSize = request.PageSize
        };

        var result = _complianceAlertService.QueryAlerts(sanitized);
        return Ok(new ApiResponse<ComplianceAlertQueryPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = result
        });
    }

    [HttpPost("financial-data/snapshots/upsert")]
    public IActionResult UpsertFinancialMarketSnapshot([FromBody] FinancialMarketSnapshotUpsertRequest request)
    {
        var sanitized = new FinancialMarketSnapshotUpsertRequest
        {
            SourceName = request.SourceName?.Trim() ?? string.Empty,
            CapturedAtUtc = request.CapturedAtUtc,
            VolatilityIndex = request.VolatilityIndex,
            CreditSpreadBps = request.CreditSpreadBps,
            FxVolatilityPercent = request.FxVolatilityPercent,
            LiquidityStressLevel = request.LiquidityStressLevel?.Trim() ?? "medium",
            Metadata = request.Metadata
        };

        if (string.IsNullOrWhiteSpace(sanitized.SourceName))
        {
            return BadRequest(new ApiResponse<object> { Code = "COMPLIANCE_4006", Msg = "sourceName 為必填" });
        }

        var snapshot = _financialMarketDataService.Upsert(sanitized);
        return Ok(new ApiResponse<FinancialMarketSnapshot>
        {
            Code = "0000",
            Msg = "金融市場快照寫入成功",
            Payload = snapshot
        });
    }

    [HttpPost("financial-data/snapshots/query")]
    public IActionResult QueryFinancialMarketSnapshots([FromBody] FinancialMarketSnapshotQueryRequest request)
    {
        var sanitized = new FinancialMarketSnapshotQueryRequest
        {
            SourceName = request.SourceName?.Trim(),
            FromCapturedAtUtc = request.FromCapturedAtUtc,
            ToCapturedAtUtc = request.ToCapturedAtUtc,
            Page = request.Page,
            PageSize = request.PageSize
        };

        var result = _financialMarketDataService.Query(sanitized);
        return Ok(new ApiResponse<FinancialMarketSnapshotQueryPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = result
        });
    }

    [HttpPost("predictive-risk/assess")]
    public IActionResult AssessPredictiveRisk([FromBody] PredictiveComplianceRiskAssessRequest request)
    {
        var sanitized = new PredictiveComplianceRiskAssessRequest
        {
            LookbackDays = request.LookbackDays,
            ForecastDays = request.ForecastDays,
            Source = request.Source?.Trim(),
            DocumentCode = request.DocumentCode?.Trim(),
            FocusAreas = request.FocusAreas?
                .Select(x => x?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToList()
        };

        var result = _predictiveComplianceRiskService.Assess(sanitized);
        return Ok(new ApiResponse<PredictiveComplianceRiskReport>
        {
            Code = "0000",
            Msg = "預測性合規風險評估完成",
            Payload = result
        });
    }

    [HttpPost("predictive-risk/query")]
    public IActionResult QueryPredictiveRisk([FromBody] PredictiveComplianceRiskQueryRequest request)
    {
        var sanitized = new PredictiveComplianceRiskQueryRequest
        {
            RiskLevel = request.RiskLevel?.Trim(),
            FromGeneratedAtUtc = request.FromGeneratedAtUtc,
            ToGeneratedAtUtc = request.ToGeneratedAtUtc,
            Page = request.Page,
            PageSize = request.PageSize
        };

        var result = _predictiveComplianceRiskService.Query(sanitized);
        return Ok(new ApiResponse<PredictiveComplianceRiskQueryPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = result
        });
    }

    [HttpPost("blockchain/anchors/commit")]
    public IActionResult CommitBlockchainAnchor([FromBody] BlockchainAuditAnchorCommitRequest request)
    {
        var sanitized = new BlockchainAuditAnchorCommitRequest
        {
            AnchorType = request.AnchorType?.Trim() ?? "audit_trail",
            Network = request.Network?.Trim() ?? "sandbox-ledger",
            PayloadHash = request.PayloadHash?.Trim(),
            Summary = request.Summary?.Trim() ?? string.Empty,
            AuditTrailIds = request.AuditTrailIds,
            Metadata = request.Metadata
        };

        var result = _blockchainComplianceService.CommitAuditAnchor(sanitized);
        return Ok(new ApiResponse<BlockchainAuditAnchorRecord>
        {
            Code = "0000",
            Msg = "區塊鏈稽核錨點寫入成功（探索）",
            Payload = result
        });
    }

    [HttpPost("blockchain/anchors/query")]
    public IActionResult QueryBlockchainAnchors([FromBody] BlockchainAuditAnchorQueryRequest request)
    {
        var sanitized = new BlockchainAuditAnchorQueryRequest
        {
            AnchorType = request.AnchorType?.Trim(),
            Network = request.Network?.Trim(),
            FromCreatedAtUtc = request.FromCreatedAtUtc,
            ToCreatedAtUtc = request.ToCreatedAtUtc,
            Page = request.Page,
            PageSize = request.PageSize
        };

        var result = _blockchainComplianceService.QueryAuditAnchors(sanitized);
        return Ok(new ApiResponse<BlockchainAuditAnchorQueryPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = result
        });
    }

    [HttpPost("blockchain/sharing/simulate")]
    public IActionResult SimulateBlockchainDataSharing([FromBody] BlockchainDataSharingSimulationRequest request)
    {
        var sanitized = new BlockchainDataSharingSimulationRequest
        {
            SourceInstitution = request.SourceInstitution?.Trim() ?? string.Empty,
            TargetInstitution = request.TargetInstitution?.Trim() ?? string.Empty,
            Regulator = request.Regulator?.Trim(),
            Purpose = request.Purpose?.Trim(),
            Fields = request.Fields
        };

        if (string.IsNullOrWhiteSpace(sanitized.SourceInstitution) || string.IsNullOrWhiteSpace(sanitized.TargetInstitution))
        {
            return BadRequest(new ApiResponse<object>
            {
                Code = "COMPLIANCE_4005",
                Msg = "sourceInstitution / targetInstitution 為必填"
            });
        }

        var result = _blockchainComplianceService.SimulateDataSharing(sanitized);
        return Ok(new ApiResponse<BlockchainDataSharingSimulationResult>
        {
            Code = "0000",
            Msg = "區塊鏈共享方案模擬完成（探索）",
            Payload = result
        });
    }

    /// <summary>
    /// 建立標準化合規證明並上鏈
    /// </summary>
    [HttpPost("proofs")]
    public async Task<IActionResult> CreateProof([FromBody] CreateComplianceProofRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BankCode) ||
            string.IsNullOrWhiteSpace(request.ReportId) ||
            string.IsNullOrWhiteSpace(request.ReportYear) ||
            string.IsNullOrWhiteSpace(request.RequestId))
        {
            return BadRequest(new { code = "4000", msg = "bankCode、reportId、reportYear、requestId 為必填" });
        }

        var sanitized = new CreateComplianceProofRequest
        {
            BankCode = request.BankCode.Trim(),
            ReportId = request.ReportId.Trim(),
            ReportYear = request.ReportYear.Trim(),
            ReportMonth = request.ReportMonth?.Trim(),
            RequestId = request.RequestId.Trim(),
            CorrelationId = request.CorrelationId?.Trim(),
            ReportPayload = request.ReportPayload
        };

        var result = await _complianceProofService.CreateProofAsync(sanitized);
        return Ok(result);
    }

    /// <summary>
    /// 依 proofId 查詢證明
    /// </summary>
    [HttpGet("proofs/{proofId}")]
    public async Task<IActionResult> GetProofById([FromRoute] string proofId)
    {
        if (string.IsNullOrWhiteSpace(proofId))
        {
            return BadRequest(new { code = "4000", msg = "proofId 為必填" });
        }

        var result = await _complianceProofService.GetProofByIdAsync(proofId.Trim());
        return result.Code == "0000" ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// 依 transactionId 查詢證明
    /// </summary>
    [HttpGet("proofs/tx/{transactionId}")]
    public async Task<IActionResult> GetProofByTransactionId([FromRoute] string transactionId)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return BadRequest(new { code = "4000", msg = "transactionId 為必填" });
        }

        var result = await _complianceProofService.GetProofByTransactionIdAsync(transactionId.Trim());
        return result.Code == "0000" ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// 依 correlationId 查詢稽核軌跡
    /// </summary>
    [HttpGet("audit/{correlationId}")]
    public async Task<IActionResult> GetAuditTrail([FromRoute] string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return BadRequest(new { code = "4000", msg = "correlationId 為必填" });
        }

        var result = await _complianceProofService.GetAuditTrailByCorrelationIdAsync(correlationId.Trim());
        return result.Code == "0000" ? Ok(result) : NotFound(result);
    }

    private static Dictionary<string, string>? ToCaseInsensitiveMappings(Dictionary<string, string>? fieldMappings)
    {
        if (fieldMappings is null)
        {
            return null;
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in fieldMappings)
        {
            var key = mapping.Key?.Trim();
            var value = mapping.Value?.Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            normalized[key] = value;
        }

        return normalized;
    }
}
