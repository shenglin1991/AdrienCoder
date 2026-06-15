using System.Collections.Concurrent;

namespace AdrienCoder.Server.Features.Workers;

public sealed class WorkerRegistry
{
    private readonly ConcurrentDictionary<string, WorkerSession> _workers =
        new(StringComparer.Ordinal);

    public WorkerSession Register(
        string workerId,
        string workerName,
        string model)
    {
        var session = new WorkerSession(workerId, workerName, model);

        if (_workers.TryGetValue(workerId, out var previous))
        {
            previous.Outgoing.Writer.TryComplete(
                new IOException("Worker reconnected with a new session."));
        }

        _workers[workerId] = session;
        return session;
    }

    public WorkerSession? GetAvailable()
    {
        return _workers.Values.FirstOrDefault(worker => worker.IsHealthy);
    }

    public void Unregister(WorkerSession session)
    {
        _workers.TryRemove(
            new KeyValuePair<string, WorkerSession>(
                session.WorkerId,
                session));
        session.Outgoing.Writer.TryComplete();
    }
}
