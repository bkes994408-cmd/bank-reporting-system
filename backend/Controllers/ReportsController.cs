using Microsoft.AspNetCore.Mvc;
using System.Globalization;
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
        if (string.IsNullOrWhiteSpace(request.BankCode))
        {
            return BadRequest(new { code = "4000", msg = "銀行代碼不可為空" });
        }

        var now = DateTime.Now;
        var rocYear = (now.Year - 1911).ToString(CultureInfo.InvariantCulture);

        request.BankCode = request.BankCode.Trim();
        request.ApplyYear = string.IsNullOrWhiteSpace(request.ApplyYear)
            ? rocYear
            : request.ApplyYear.Trim();

        request.ApplyMonth = NormalizeMonthOrDefault(request.ApplyMonth, now.Month);

        var result = await _agentService.GetMonthlyReportsAsync(request);
        return Ok(result);
    }

    private static string NormalizeMonthOrDefault(string? month, int defaultMonth)
    {
        if (string.IsNullOrWhiteSpace(month))
        {
            return defaultMonth.ToString("D2", CultureInfo.InvariantCulture);
        }

        if (!int.TryParse(month, out var parsedMonth) || parsedMonth < 1 || parsedMonth > 12)
        {
            return defaultMonth.ToString("D2", CultureInfo.InvariantCulture);
        }

        return parsedMonth.ToString("D2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 查詢報表申報歷程
    /// </summary>
    [HttpPost("histories")]
    public async Task<IActionResult> GetReportHistories([FromBody] ReportHistoriesRequest request)
    {
        var result = await _agentService.GetReportHistoriesAsync(request);
        return Ok(result);
    }
}
