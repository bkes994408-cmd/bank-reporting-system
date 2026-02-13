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
        if (string.IsNullOrEmpty(request.Token))
        {
            return BadRequest(new { code = "4000", msg = "Token為必填" });
        }

        var result = await _agentService.UpdateTokenAsync(request);
        return Ok(result);
    }
}
