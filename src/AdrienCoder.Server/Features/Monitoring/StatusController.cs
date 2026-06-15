using AdrienCoder.Server.Features.Monitoring.Models;
using AdrienCoder.Server.Features.Monitoring.Services;
using AdrienCoder.Server.Features.Llm.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Server.Features.Monitoring;

[ApiController]
[Route("api/status")]
public sealed class StatusController : ControllerBase
{
    private readonly MonitoringService _monitoringService;
    private readonly ILLMService _llmService;

    public StatusController(
        MonitoringService monitoringService,
        ILLMService llmService)
    {
        _monitoringService = monitoringService;
        _llmService = llmService;
    }

    [HttpGet]
    public async Task<ActionResult<AppStatusResponse>> Get()
    {
        return Ok(await _monitoringService.GetStatusAsync());
    }

    [HttpGet("models")]
    public async Task<IActionResult> GetModels()
    {
        var models = await _llmService.GetModelsAsync();
        return Content(models, "application/json");
    }
}
