using Microsoft.AspNetCore.Mvc;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly IReportHistoryArchiveService _archiveService;

    public ReportsController(IAgentService agentService, IReportHistoryArchiveService archiveService)
    {
        _agentService = agentService;
        _archiveService = archiveService;
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

    /// <summary>
    /// 歷史資料歸檔（從上游查詢後封存）
    /// </summary>
    [HttpPost("histories/archive")]
    public async Task<IActionResult> ArchiveReportHistories([FromBody] ReportHistoriesRequest request)
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

        var archivedCount = await _archiveService.ArchiveAsync(sanitizedRequest, CancellationToken.None);
        return Ok(new ApiResponse<object>
        {
            Code = "0000",
            Msg = "歸檔成功",
            Payload = new { archivedCount }
        });
    }

    /// <summary>
    /// 歷史資料歸檔查詢（支援條件篩選與分頁）
    /// </summary>
    [HttpPost("histories/archive/query")]
    public IActionResult QueryArchivedReportHistories([FromBody] ArchivedReportHistoriesQueryRequest request)
    {
        var sanitizedRequest = new ArchivedReportHistoriesQueryRequest
        {
            BankCode = request.BankCode?.Trim(),
            ReportId = request.ReportId?.Trim(),
            Year = request.Year?.Trim(),
            Type = request.Type?.Trim(),
            Status = request.Status?.Trim(),
            Page = request.Page,
            PageSize = request.PageSize
        };

        var result = _archiveService.Query(sanitizedRequest);
        return Ok(new ApiResponse<ArchivedReportHistoriesPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = result
        });
    }
}
