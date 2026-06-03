using AdrienCoder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModelsController : ControllerBase
{
    private readonly OllamaService _ollamaService;

    public ModelsController(OllamaService ollamaService)
    {
        _ollamaService = ollamaService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var models = await _ollamaService.GetModelsAsync();
        return Content(models, "application/json");
    }
}