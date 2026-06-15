using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Server.Features.Workers;

[ApiController]
[Route("api/workers")]
public sealed class WorkersController : ControllerBase
{
    private readonly WorkerRegistry _workerRegistry;

    public WorkersController(WorkerRegistry workerRegistry)
    {
        _workerRegistry = workerRegistry;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var workers = _workerRegistry.GetStatuses();

        return Ok(new
        {
            connected = workers.Count,
            healthy = workers.Count(worker => worker.Healthy),
            workers
        });
    }
}
