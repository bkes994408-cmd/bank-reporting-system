using Microsoft.AspNetCore.Mvc;
using BankReporting.Api.DTOs;
using BankReporting.Api.Services;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("api/token")]
public class TokenController : ControllerBase
{
    private readonly IAgentService _agentService;

    public TokenController(IAgentService agentService)
    {
        _agentService = agentService;
    }

    /// <summary>
    /// 更新Token
    /// </summary>
    [HttpPost("update")]
    public async Task<IActionResult> UpdateToken([FromBody] UpdateTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { code = "4000", msg = "Token為必填" });
        }

        if (request.Token.Length > 2048)
        {
            return BadRequest(new { code = "4000", msg = "Token長度不可超過2048字元" });
        }

        var sanitizedRequest = new UpdateTokenRequest
        {
            Token = request.Token.Trim()
        };

        var result = await _agentService.UpdateTokenAsync(sanitizedRequest);
        return Ok(result);
    }
}
