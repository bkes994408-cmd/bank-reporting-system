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

    public ComplianceController(IComplianceAuditService complianceAuditService)
    {
        _complianceAuditService = complianceAuditService;
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
}
