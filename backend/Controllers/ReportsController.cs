using Microsoft.AspNetCore.Mvc;
using BankReporting.Api.DTOs;
using BankReporting.Api.Services;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IAgentService _agentService;

    public ReportsController(IAgentService agentService)
    {
        _agentService = agentService;
    }

    /// <summary>
    /// 查詢當月應申報報表及狀態
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GetMonthlyReports([FromBody] MonthlyReportsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BankCode) ||
            string.IsNullOrWhiteSpace(request.ApplyYear))
        {
            return BadRequest(new { code = "4000", msg = "bankCode 與 applyYear 為必填" });
        }

        if (!string.IsNullOrWhiteSpace(request.ApplyMonth))
        {
            if (!int.TryParse(request.ApplyMonth, out var monthNumber) || monthNumber < 1 || monthNumber > 12)
            {
                return BadRequest(new { code = "4000", msg = "applyMonth 必須為 01~12" });
            }
        }

        var sanitizedRequest = new MonthlyReportsRequest
        {
            BankCode = request.BankCode.Trim(),
            ApplyYear = request.ApplyYear.Trim(),
            ApplyMonth = request.ApplyMonth?.Trim()
        };

        var result = await _agentService.GetMonthlyReportsAsync(sanitizedRequest);
        return Ok(result);
    }

    /// <summary>
    /// 查詢報表申報歷程
    /// </summary>
    [HttpPost("histories")]
    public async Task<IActionResult> GetReportHistories([FromBody] ReportHistoriesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BankCode) ||
            string.IsNullOrWhiteSpace(request.ReportId) ||
            string.IsNullOrWhiteSpace(request.Year))
        {
            return BadRequest(new { code = "4000", msg = "bankCode、reportId、year 為必填" });
        }

        var sanitizedRequest = new ReportHistoriesRequest
        {
            BankCode = request.BankCode.Trim(),
            ReportId = request.ReportId.Trim(),
            Year = request.Year.Trim(),
            Type = request.Type?.Trim()
        };

        var result = await _agentService.GetReportHistoriesAsync(sanitizedRequest);
        return Ok(result);
    }
}
