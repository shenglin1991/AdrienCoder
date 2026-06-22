using AdrienCoder.Contracts.Worker;
using Grpc.Core;

namespace AdrienCoder.Server.Features.Workers;

public sealed class WorkerGatewayService : WorkerGateway.WorkerGatewayBase
{
    private readonly WorkerRegistry _workerRegistry;
    private readonly GpuJobDispatcher _dispatcher;
    private readonly ILogger<WorkerGatewayService> _logger;

    public WorkerGatewayService(
        WorkerRegistry workerRegistry,
        GpuJobDispatcher dispatcher,
        ILogger<WorkerGatewayService> logger)
    {
        _workerRegistry = workerRegistry;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public override async Task Connect(
        IAsyncStreamReader<WorkerMessage> requestStream,
        IServerStreamWriter<ServerMessage> responseStream,
        ServerCallContext context)
    {
        if (!await requestStream.MoveNext(context.CancellationToken)
            || requestStream.Current.PayloadCase
                != WorkerMessage.PayloadOneofCase.Registration)
        {
            throw new RpcException(
                new Status(
                    StatusCode.InvalidArgument,
                    "The first worker message must be a registration."));
        }

        var registration = requestStream.Current.Registration;
        var session = _workerRegistry.Register(
            registration.WorkerId,
            registration.WorkerName,
            registration.Model);

        _logger.LogInformation(
            "GPU worker {WorkerId} connected with model {Model}.",
            session.WorkerId,
            session.Model);

        var writerTask = WriteJobsAsync(
            session,
            responseStream,
            context.CancellationToken);

        try
        {
            while (await requestStream.MoveNext(context.CancellationToken))
            {
                var message = requestStream.Current;

                switch (message.PayloadCase)
                {
                    case WorkerMessage.PayloadOneofCase.Heartbeat:
                        session.MarkHeartbeat(
                            message.Heartbeat.UnixTimestamp);
                        break;
                    case WorkerMessage.PayloadOneofCase.JobResult:
                        _dispatcher.Complete(message.JobResult);
                        break;
                    case WorkerMessage.PayloadOneofCase.JobChunk:
                        _dispatcher.AddChunk(message.JobChunk);
                        break;
                }
            }
        }
        finally
        {
            _workerRegistry.Unregister(session);
            _dispatcher.FailJobs(session.WorkerId);

            try
            {
                await writerTask;
            }
            catch (OperationCanceledException)
            {
            }

            _logger.LogWarning(
                "GPU worker {WorkerId} disconnected.",
                session.WorkerId);
        }
    }

    private static async Task WriteJobsAsync(
        WorkerSession session,
        IServerStreamWriter<ServerMessage> responseStream,
        CancellationToken cancellationToken)
    {
        await foreach (var message in session.Outgoing.Reader.ReadAllAsync(
            cancellationToken))
        {
            await responseStream.WriteAsync(message);
        }
    }
}
