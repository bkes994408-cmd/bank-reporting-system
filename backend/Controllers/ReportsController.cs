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
    private readonly IEncryptedExportArchiveService _encryptedExportArchiveService;

    public ReportsController(
        IAgentService agentService,
        IReportHistoryArchiveService archiveService,
        IEncryptedExportArchiveService encryptedExportArchiveService)
    {
        _agentService = agentService;
        _archiveService = archiveService;
        _encryptedExportArchiveService = encryptedExportArchiveService;
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

    /// <summary>
    /// 以 AES-GCM 加密封存報表歷程匯出資料
    /// </summary>
    [HttpPost("secure-archive/report-histories")]
    public async Task<IActionResult> ArchiveReportHistoriesEncrypted([FromBody] ReportHistoriesRequest request)
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

        var record = await _encryptedExportArchiveService.ArchiveReportHistoriesAsync(sanitizedRequest, CancellationToken.None);
        return Ok(new ApiResponse<object>
        {
            Code = "0000",
            Msg = "加密封存成功",
            Payload = new
            {
                record.ArchiveId,
                record.Category,
                record.BankCode,
                record.ReportId,
                record.Year,
                record.DataSha256Hex,
                record.ArchivedAtUtc
            }
        });
    }

    /// <summary>
    /// 以 AES-GCM 加密封存申報結果匯出資料
    /// </summary>
    [HttpPost("secure-archive/declare-result")]
    public async Task<IActionResult> ArchiveDeclareResultEncrypted([FromBody] DeclareResultRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId) && string.IsNullOrWhiteSpace(request.TransactionId))
        {
            return BadRequest(new { code = "4000", msg = "requestId 或 transactionId 至少需填一個" });
        }

        var sanitizedRequest = new DeclareResultRequest
        {
            RequestId = request.RequestId?.Trim(),
            TransactionId = request.TransactionId?.Trim()
        };

        var record = await _encryptedExportArchiveService.ArchiveDeclareResultAsync(sanitizedRequest, CancellationToken.None);
        return Ok(new ApiResponse<object>
        {
            Code = "0000",
            Msg = "加密封存成功",
            Payload = new
            {
                record.ArchiveId,
                record.Category,
                record.BankCode,
                record.ReportId,
                record.Year,
                record.RequestIdMasked,
                record.TransactionIdMasked,
                record.DataSha256Hex,
                record.ArchivedAtUtc
            }
        });
    }

    /// <summary>
    /// 查詢加密封存紀錄（僅回傳遮罩後 metadata）
    /// </summary>
    [HttpPost("secure-archive/query")]
    public IActionResult QueryEncryptedArchive([FromBody] EncryptedArchiveQueryRequest request)
    {
        var sanitizedRequest = new EncryptedArchiveQueryRequest
        {
            Category = request.Category?.Trim(),
            BankCode = request.BankCode?.Trim(),
            ReportId = request.ReportId?.Trim(),
            RequestId = request.RequestId?.Trim(),
            TransactionId = request.TransactionId?.Trim(),
            StartDateUtc = request.StartDateUtc,
            EndDateUtc = request.EndDateUtc,
            Page = request.Page,
            PageSize = request.PageSize
        };

        var result = _encryptedExportArchiveService.Query(sanitizedRequest);
        return Ok(new ApiResponse<EncryptedArchiveQueryPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = result
        });
    }
}
