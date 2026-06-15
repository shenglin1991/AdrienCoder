using AdrienCoder.Server.Features.Monitoring.Models;
using AdrienCoder.Server.Features.Monitoring.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Server.Features.Monitoring;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly MonitoringService _monitoringService;

    public StatusController(MonitoringService monitoringService)
    {
        _monitoringService = monitoringService;
    }

    [HttpGet]
    public async Task<ActionResult<AppStatusResponse>> Get()
    {
        return Ok(await _monitoringService.GetStatusAsync());
    }
}
