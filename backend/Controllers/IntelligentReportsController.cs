using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("api/compliance/intelligent-reports")]
public class IntelligentReportsController : ControllerBase
{
    private readonly IIntelligentReportAutomationService _intelligentReportAutomationService;

    public IntelligentReportsController(IIntelligentReportAutomationService intelligentReportAutomationService)
    {
        _intelligentReportAutomationService = intelligentReportAutomationService;
    }

    [HttpPost("auto-submit")]
    public async Task<IActionResult> AutoSubmit([FromBody] IntelligentReportAutoSubmitRequest request)
    {
        var sanitized = new IntelligentReportAutoSubmitRequest
        {
            BankCode = request.BankCode?.Trim() ?? string.Empty,
            BankName = request.BankName?.Trim() ?? string.Empty,
            ReportYear = request.ReportYear?.Trim() ?? string.Empty,
            ReportMonth = request.ReportMonth?.Trim() ?? string.Empty,
            ReportId = request.ReportId?.Trim() ?? string.Empty,
            ContractorName = request.ContractorName?.Trim() ?? string.Empty,
            ContractorTel = request.ContractorTel?.Trim() ?? string.Empty,
            ContractorEmail = request.ContractorEmail?.Trim() ?? string.Empty,
            ManagerName = request.ManagerName?.Trim() ?? string.Empty,
            ManagerTel = request.ManagerTel?.Trim() ?? string.Empty,
            ManagerEmail = request.ManagerEmail?.Trim() ?? string.Empty,
            DryRun = request.DryRun,
            EnablePredictiveRiskAssessment = request.EnablePredictiveRiskAssessment,
            PredictiveLookbackDays = request.PredictiveLookbackDays,
            PredictiveForecastDays = request.PredictiveForecastDays,
            PredictiveFocusAreas = request.PredictiveFocusAreas?
                .Select(x => x?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToList(),
            SourceData = request.SourceData
        };

        if (string.IsNullOrWhiteSpace(sanitized.BankCode) ||
            string.IsNullOrWhiteSpace(sanitized.BankName) ||
            string.IsNullOrWhiteSpace(sanitized.ReportYear) ||
            string.IsNullOrWhiteSpace(sanitized.ReportMonth) ||
            string.IsNullOrWhiteSpace(sanitized.ReportId) ||
            string.IsNullOrWhiteSpace(sanitized.ContractorName) ||
            string.IsNullOrWhiteSpace(sanitized.ContractorTel) ||
            string.IsNullOrWhiteSpace(sanitized.ContractorEmail) ||
            string.IsNullOrWhiteSpace(sanitized.ManagerName) ||
            string.IsNullOrWhiteSpace(sanitized.ManagerTel) ||
            string.IsNullOrWhiteSpace(sanitized.ManagerEmail))
        {
            return BadRequest(new ApiResponse<object>
            {
                Code = "COMPLIANCE_4006",
                Msg = "自動提交欄位不完整"
            });
        }

        try
        {
            var result = await _intelligentReportAutomationService.AutoGenerateAndSubmitAsync(sanitized, CancellationToken.None);
            return Ok(new ApiResponse<IntelligentReportSubmissionRecord>
            {
                Code = "0000",
                Msg = sanitized.DryRun ? "智能報表模板生成完成（dry-run）" : "智能報表自動生成與提交完成",
                Payload = result
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                Code = "COMPLIANCE_4007",
                Msg = ex.Message
            });
        }
    }

    [HttpPost("query")]
    public IActionResult Query([FromBody] IntelligentReportSubmissionQueryRequest request)
    {
        var sanitized = new IntelligentReportSubmissionQueryRequest
        {
            ReportId = request.ReportId?.Trim(),
            Status = request.Status?.Trim(),
            FromGeneratedAtUtc = request.FromGeneratedAtUtc,
            ToGeneratedAtUtc = request.ToGeneratedAtUtc,
            Page = request.Page,
            PageSize = request.PageSize
        };

        var payload = _intelligentReportAutomationService.Query(sanitized);
        return Ok(new ApiResponse<IntelligentReportSubmissionQueryPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = payload
        });
    }
}
