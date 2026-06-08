using AdrienCoder.Api.Models;
using AdrienCoder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AskController : ControllerBase
{
    private readonly ILLMService _llmService;

    public AskController(ILLMService llmService)
    {
        _llmService = llmService;
    }

    [HttpPost]
    public async Task<ActionResult<AskResponse>> Ask([FromBody] AskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required.");
        }

        var answer = await _llmService.AskAsync(request.Question);

        return Ok(new AskResponse
        {
            Answer = answer
        });
    }
}