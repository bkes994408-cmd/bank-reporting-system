using BankReporting.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("")]
public class MonitoringController : ControllerBase
{
    private readonly IMonitoringService _monitoringService;

    public MonitoringController(IMonitoringService monitoringService)
    {
        _monitoringService = monitoringService;
    }

    [HttpGet("metrics")]
    public IActionResult Metrics()
    {
        var metrics = _monitoringService.BuildPrometheusMetrics();
        return Content(metrics, "text/plain; version=0.0.4");
    }
}
