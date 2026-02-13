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
        var result = await _agentService.DeclareAsync(request);
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
