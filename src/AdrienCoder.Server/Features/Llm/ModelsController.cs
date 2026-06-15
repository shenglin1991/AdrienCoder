using AdrienCoder.Server.Features.Llm.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Server.Features.Llm;

[ApiController]
[Route("api/[controller]")]
public class ModelsController : ControllerBase
{
    private readonly ILLMService _llmService;

    public ModelsController(ILLMService llmService)
    {
        _llmService = llmService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var models = await _llmService.GetModelsAsync();
        return Content(models, "application/json");
    }
}
