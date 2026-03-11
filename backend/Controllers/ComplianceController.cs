using System.ComponentModel.DataAnnotations;
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

    public ComplianceController(IComplianceAuditService complianceAuditService, IRegulationMonitoringService regulationMonitoringService)
    {
        _complianceAuditService = complianceAuditService;
        _regulationMonitoringService = regulationMonitoringService;
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
            Content = request.Content?.Trim() ?? string.Empty,
            PublishedAtUtc = request.PublishedAtUtc,
            Url = request.Url?.Trim()
        };

        if (!TryValidateRequest(sanitized, out var errors))
        {
            return BadRequest(new ApiResponse<object>
            {
                Code = "COMPLIANCE_4000",
                Msg = string.Join("；", errors)
            });
        }

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

        if (!TryValidateRequest(sanitized, out var errors))
        {
            return BadRequest(new ApiResponse<object>
            {
                Code = "COMPLIANCE_4000",
                Msg = string.Join("；", errors)
            });
        }

        try
        {
            var report = await _regulationMonitoringService.AnalyzeLatestAsync(sanitized, HttpContext?.RequestAborted ?? CancellationToken.None);
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
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status408RequestTimeout, new ApiResponse<object>
            {
                Code = "COMPLIANCE_4008",
                Msg = "請求已取消或逾時"
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

        if (!TryValidateRequest(sanitized, out var errors))
        {
            return BadRequest(new ApiResponse<object>
            {
                Code = "COMPLIANCE_4000",
                Msg = string.Join("；", errors)
            });
        }

        var result = _regulationMonitoringService.QueryImpactReports(sanitized);
        return Ok(new ApiResponse<RegulationImpactQueryPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = result
        });
    }

    private static bool TryValidateRequest<T>(T request, out List<string> errors)
    {
        var context = new ValidationContext(request!);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request!, context, validationResults, validateAllProperties: true);

        errors = validationResults
            .Select(x => x.ErrorMessage)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return isValid;
    }
}
