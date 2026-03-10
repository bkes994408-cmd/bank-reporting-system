using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("api/integrations/third-party")]
public class ThirdPartyIntegrationsController : ControllerBase
{
    private readonly IThirdPartyIntegrationService _service;

    public ThirdPartyIntegrationsController(IThirdPartyIntegrationService service)
    {
        _service = service;
    }

    [HttpGet("systems")]
    public IActionResult GetSystems()
    {
        return Ok(new ApiResponse<ThirdPartySystemsPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = new ThirdPartySystemsPayload
            {
                Systems = _service.GetEnabledSystems()
            }
        });
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] ThirdPartySyncRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SystemName) ||
            string.IsNullOrWhiteSpace(request.EventType) ||
            string.IsNullOrWhiteSpace(request.BankCode) ||
            string.IsNullOrWhiteSpace(request.ReportId) ||
            string.IsNullOrWhiteSpace(request.Period) ||
            string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { code = "4000", msg = "同步欄位不完整" });
        }

        request.SystemName = request.SystemName.Trim();
        request.EventType = request.EventType.Trim();
        request.BankCode = request.BankCode.Trim();
        request.ReportId = request.ReportId.Trim();
        request.Period = request.Period.Trim();
        request.Status = request.Status.Trim();
        request.RequestId = request.RequestId?.Trim();
        request.TransactionId = request.TransactionId?.Trim();

        var result = await _service.SyncAsync(request);
        if (result.Code == "4040")
        {
            return NotFound(result);
        }

        return Ok(result);
    }
}
