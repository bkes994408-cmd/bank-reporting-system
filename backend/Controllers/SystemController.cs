using Microsoft.AspNetCore.Mvc;
using BankReporting.Api.Services;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("api")]
public class SystemController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly IConfiguration _configuration;

    public SystemController(IAgentService agentService, IConfiguration configuration)
    {
        _agentService = agentService;
        _configuration = configuration;
    }

    /// <summary>
    /// 檢查版本
    /// </summary>
    [HttpGet("check-version")]
    public async Task<IActionResult> CheckVersion()
    {
        var result = await _agentService.CheckVersionAsync();
        return Ok(result);
    }

    /// <summary>
    /// 查詢代理程式資訊
    /// </summary>
    [HttpGet("info")]
    public async Task<IActionResult> GetInfo()
    {
        var result = await _agentService.GetAgentInfoAsync();
        return Ok(result);
    }

    /// <summary>
    /// 取得系統設定
    /// </summary>
    [HttpGet("settings")]
    public IActionResult GetSettings()
    {
        var settings = new
        {
            apiServerUrl = _configuration["AgentSettings:BaseUrl"],
            autoUpdateTime = _configuration["AgentSettings:AutoUpdateTime"]
        };
        return Ok(new { code = "0000", msg = "查詢成功", payload = settings });
    }

    /// <summary>
    /// 更新系統設定
    /// </summary>
    [HttpPost("settings")]
    public IActionResult UpdateSettings([FromBody] SystemSettingsRequest request)
    {
        // In a real application, this would update the configuration
        return Ok(new { code = "0000", msg = "更新成功" });
    }
}

public class SystemSettingsRequest
{
    public string? ApiServerUrl { get; set; }
    public string? AutoUpdateTime { get; set; }
}
