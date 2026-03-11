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

    public ComplianceController(
        IComplianceAuditService complianceAuditService,
        IRegulationMonitoringService regulationMonitoringService,
        IExternalComplianceDataService externalComplianceDataService)
    {
        _complianceAuditService = complianceAuditService;
        _regulationMonitoringService = regulationMonitoringService;
        _externalComplianceDataService = externalComplianceDataService;
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
