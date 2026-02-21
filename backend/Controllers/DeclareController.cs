using Microsoft.AspNetCore.Mvc;
using BankReporting.Api.DTOs;
using BankReporting.Api.Services;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("api/declare")]
public class DeclareController : ControllerBase
{
    private readonly IAgentService _agentService;

    public DeclareController(IAgentService agentService)
    {
        _agentService = agentService;
    }

    /// <summary>
    /// 上傳申報表
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Declare([FromBody] DeclareRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId) ||
            string.IsNullOrWhiteSpace(request.BankCode) ||
            string.IsNullOrWhiteSpace(request.BankName) ||
            string.IsNullOrWhiteSpace(request.ReportYear) ||
            string.IsNullOrWhiteSpace(request.ReportMonth) ||
            string.IsNullOrWhiteSpace(request.ReportId) ||
            string.IsNullOrWhiteSpace(request.ContractorName) ||
            string.IsNullOrWhiteSpace(request.ContractorTel) ||
            string.IsNullOrWhiteSpace(request.ContractorEmail) ||
            string.IsNullOrWhiteSpace(request.ManagerName) ||
            string.IsNullOrWhiteSpace(request.ManagerTel) ||
            string.IsNullOrWhiteSpace(request.ManagerEmail))
        {
            return BadRequest(new { code = "4000", msg = "申報欄位不完整" });
        }

        if (request.Report == null && string.IsNullOrWhiteSpace(request.JwePayload))
        {
            return BadRequest(new { code = "4000", msg = "report 或 jwePayload 至少需填一個" });
        }

        if (request.UseSignature && string.IsNullOrWhiteSpace(request.Signature))
        {
            return BadRequest(new { code = "4000", msg = "啟用簽章時，signature 為必填" });
        }

        if (request.UseJwe && string.IsNullOrWhiteSpace(request.JwePayload))
        {
            return BadRequest(new { code = "4000", msg = "啟用 JWE 時，jwePayload 為必填" });
        }

        var sanitizedRequest = new DeclareRequest
        {
            RequestId = request.RequestId.Trim(),
            BankCode = request.BankCode.Trim(),
            BankName = request.BankName.Trim(),
            ReportYear = request.ReportYear.Trim(),
            ReportMonth = request.ReportMonth.Trim(),
            ReportId = request.ReportId.Trim(),
            ContractorName = request.ContractorName.Trim(),
            ContractorTel = request.ContractorTel.Trim(),
            ContractorEmail = request.ContractorEmail.Trim(),
            ManagerName = request.ManagerName.Trim(),
            ManagerTel = request.ManagerTel.Trim(),
            ManagerEmail = request.ManagerEmail.Trim(),
            Report = request.Report,
            UseSignature = request.UseSignature,
            Signature = request.Signature?.Trim(),
            UseJwe = request.UseJwe,
            JwePayload = request.JwePayload?.Trim()
        };

        var result = await _agentService.DeclareAsync(sanitizedRequest);
        return Ok(result);
    }

    /// <summary>
    /// 查詢上傳申報結果
    /// </summary>
    [HttpPost("result")]
    public async Task<IActionResult> GetDeclareResult([FromBody] DeclareResultRequest request)
    {
        if (string.IsNullOrEmpty(request.RequestId) && string.IsNullOrEmpty(request.TransactionId))
        {
            return BadRequest(new { code = "4000", msg = "requestId 或 transactionId 至少需填一個" });
        }

        var result = await _agentService.GetDeclareResultAsync(request);
        return Ok(result);
    }
}
