using System.Collections.Concurrent;
using AdrienCoder.Contracts.Worker;

namespace AdrienCoder.Server.Features.Workers;

public sealed class GpuJobDispatcher
{
    private static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, PendingJob> _pendingJobs = [];
    private readonly WorkerRegistry _workerRegistry;

    public GpuJobDispatcher(WorkerRegistry workerRegistry)
    {
        _workerRegistry = workerRegistry;
    }

    public bool IsAvailable => _workerRegistry.GetAvailable() is not null;

    public async Task<string> DispatchAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var worker = _workerRegistry.GetAvailable()
            ?? throw new InvalidOperationException(
                "No GPU worker is currently connected.");
        var jobId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<JobResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_pendingJobs.TryAdd(
                jobId,
                new PendingJob(worker.WorkerId, completion)))
        {
            throw new InvalidOperationException("Unable to create the GPU job.");
        }

        try
        {
            await worker.Outgoing.Writer.WriteAsync(
                new ServerMessage
                {
                    Job = new WorkerJob
                    {
                        JobId = jobId,
                        Prompt = prompt
                    }
                },
                cancellationToken);

            var result = await completion.Task.WaitAsync(
                JobTimeout,
                cancellationToken);

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"GPU worker job failed: {result.Error}");
            }

            return result.Response;
        }
        finally
        {
            _pendingJobs.TryRemove(jobId, out _);
        }
    }

    public void Complete(JobResult result)
    {
        if (_pendingJobs.TryRemove(result.JobId, out var pending))
        {
            pending.Completion.TrySetResult(result);
        }
    }

    public void FailJobs(string workerId)
    {
        foreach (var pair in _pendingJobs)
        {
            if (pair.Value.WorkerId == workerId
                && _pendingJobs.TryRemove(pair.Key, out var pending))
            {
                pending.Completion.TrySetException(
                    new IOException("GPU worker disconnected."));
            }
        }
    }

    private sealed record PendingJob(
        string WorkerId,
        TaskCompletionSource<JobResult> Completion);
}
