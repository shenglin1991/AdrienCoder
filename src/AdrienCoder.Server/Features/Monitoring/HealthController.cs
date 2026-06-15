using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Server.Features.Monitoring;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            app = "AdrienCoder.Server",
            time = DateTimeOffset.UtcNow
        });
    }
}
