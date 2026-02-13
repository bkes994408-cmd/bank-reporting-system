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
        var result = await _agentService.GetMonthlyReportsAsync(request);
        return Ok(result);
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
