using BankReporting.Api.DTOs;
using BankReporting.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAdAuthService _adAuthService;

    public AuthController(IAdAuthService adAuthService)
    {
        _adAuthService = adAuthService;
    }

    /// <summary>
    /// AD 網域登入
    /// </summary>
    [HttpPost("ad-login")]
    public async Task<IActionResult> AdLogin([FromBody] AdLoginRequest request)
    {
        request.Username = request.Username?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { code = "AUTH_INVALID_INPUT", msg = "帳號或密碼不可為空" });
        }

        var result = await _adAuthService.LoginAsync(request.Username, request.Password);

        if (result.Code == "0000")
        {
            return Ok(result);
        }

        if (result.Code == "AUTH_DISABLED")
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }

        if (result.Code == "AUTH_INVALID_CREDENTIALS")
        {
            return Unauthorized(result);
        }

        return BadRequest(result);
    }
}
