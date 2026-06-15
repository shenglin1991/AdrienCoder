using System.Threading.Channels;
using AdrienCoder.Contracts.Worker;

namespace AdrienCoder.Server.Features.Workers;

public sealed class WorkerSession
{
    private long _lastHeartbeatUnixSeconds =
        DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public WorkerSession(
        string workerId,
        string workerName,
        string model)
    {
        WorkerId = workerId;
        WorkerName = workerName;
        Model = model;
        ConnectedAt = DateTimeOffset.UtcNow;
        Outgoing = Channel.CreateBounded<ServerMessage>(
            new BoundedChannelOptions(8)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
    }

    public string WorkerId { get; }
    public string WorkerName { get; }
    public string Model { get; }
    public DateTimeOffset ConnectedAt { get; }
    public Channel<ServerMessage> Outgoing { get; }

    public DateTimeOffset LastHeartbeat =>
        DateTimeOffset.FromUnixTimeSeconds(
            Interlocked.Read(ref _lastHeartbeatUnixSeconds));

    public bool IsHealthy =>
        DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        - Interlocked.Read(ref _lastHeartbeatUnixSeconds) < 90;

    public void MarkHeartbeat(long unixTimestamp)
    {
        Interlocked.Exchange(
            ref _lastHeartbeatUnixSeconds,
            Math.Max(unixTimestamp, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
    }
}
