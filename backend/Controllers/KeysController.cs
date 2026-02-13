using Microsoft.AspNetCore.Mvc;
using BankReporting.Api.DTOs;
using BankReporting.Api.Services;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("api/keys")]
public class KeysController : ControllerBase
{
    private readonly IAgentService _agentService;

    public KeysController(IAgentService agentService)
    {
        _agentService = agentService;
    }

    /// <summary>
    /// 匯入金鑰
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> ImportKeys([FromBody] ImportKeysRequest request)
    {
        if (string.IsNullOrEmpty(request.KeyA) || string.IsNullOrEmpty(request.KeyB))
        {
            return BadRequest(new { code = "4000", msg = "金鑰A和金鑰B均為必填" });
        }

        var result = await _agentService.ImportKeysAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// 驗證金鑰
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateKeys()
    {
        var result = await _agentService.ValidateKeysAsync();
        return Ok(result);
    }
}
