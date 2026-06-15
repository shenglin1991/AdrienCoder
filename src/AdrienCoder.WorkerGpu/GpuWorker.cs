using System.Threading.Channels;
using AdrienCoder.Contracts.Worker;
using AdrienCoder.WorkerGpu.Configuration;
using AdrienCoder.WorkerGpu.Services;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;

namespace AdrienCoder.WorkerGpu;

public sealed class GpuWorker : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    private readonly ServerOptions _serverOptions;
    private readonly WorkerOptions _workerOptions;
    private readonly OllamaOptions _ollamaOptions;
    private readonly ILocalLlmClient _llmClient;
    private readonly ILogger<GpuWorker> _logger;

    public GpuWorker(
        IOptions<ServerOptions> serverOptions,
        IOptions<WorkerOptions> workerOptions,
        IOptions<OllamaOptions> ollamaOptions,
        ILocalLlmClient llmClient,
        ILogger<GpuWorker> logger)
    {
        _serverOptions = serverOptions.Value;
        _workerOptions = workerOptions.Value;
        _ollamaOptions = ollamaOptions.Value;
        _llmClient = llmClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConnectionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Worker connection to {ServerBaseUrl} ended.",
                    _serverOptions.BaseUrl);
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_workerOptions.ReconnectDelaySeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunConnectionAsync(CancellationToken stoppingToken)
    {
        using var grpcChannel = GrpcChannel.ForAddress(_serverOptions.BaseUrl);
        var client = new WorkerGateway.WorkerGatewayClient(grpcChannel);
        var headers = new Metadata
        {
            { "x-api-key", _serverOptions.ApiKey }
        };

        using var connectionCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var call = client.Connect(
            headers,
            cancellationToken: connectionCancellation.Token);

        var outgoing = Channel.CreateUnbounded<WorkerMessage>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

        var writerTask = WriteOutgoingAsync(
            call.RequestStream,
            outgoing.Reader,
            connectionCancellation.Token);

        await outgoing.Writer.WriteAsync(
            CreateRegistrationMessage(),
            connectionCancellation.Token);
        await outgoing.Writer.WriteAsync(
            CreateHeartbeatMessage(),
            connectionCancellation.Token);

        _logger.LogInformation(
            "Worker {WorkerId} connected to {ServerBaseUrl} with model {Model}.",
            _workerOptions.Id,
            _serverOptions.BaseUrl,
            _ollamaOptions.Model);

        var heartbeatTask = SendHeartbeatsAsync(
            outgoing.Writer,
            connectionCancellation.Token);
        var receiverTask = ReceiveJobsAsync(
            call.ResponseStream,
            outgoing.Writer,
            connectionCancellation.Token);

        try
        {
            var completedTask = await Task.WhenAny(writerTask, receiverTask);
            await completedTask;

            if (completedTask == writerTask
                && !connectionCancellation.IsCancellationRequested)
            {
                throw new IOException("The gRPC request stream closed unexpectedly.");
            }
        }
        finally
        {
            connectionCancellation.Cancel();
            outgoing.Writer.TryComplete();

            await IgnoreCancellationAsync(writerTask);
            await IgnoreCancellationAsync(heartbeatTask);
            await IgnoreCancellationAsync(receiverTask);
        }
    }

    private async Task ReceiveJobsAsync(
        IAsyncStreamReader<ServerMessage> responseStream,
        ChannelWriter<WorkerMessage> outgoing,
        CancellationToken cancellationToken)
    {
        await foreach (var message in responseStream.ReadAllAsync(cancellationToken))
        {
            if (message.PayloadCase != ServerMessage.PayloadOneofCase.Job)
            {
                _logger.LogWarning(
                    "Received an unsupported server message of type {PayloadType}.",
                    message.PayloadCase);
                continue;
            }

            await ProcessJobAsync(message.Job, outgoing, cancellationToken);
        }
    }

    private async Task ProcessJobAsync(
        WorkerJob job,
        ChannelWriter<WorkerMessage> outgoing,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting job {JobId}.", job.JobId);

        JobResult result;

        try
        {
            var response = await _llmClient.ChatAsync(
                job.Prompt,
                cancellationToken);

            result = new JobResult
            {
                JobId = job.JobId,
                Success = true,
                Response = response
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Job {JobId} failed.", job.JobId);

            result = new JobResult
            {
                JobId = job.JobId,
                Success = false,
                Error = exception.Message
            };
        }

        await outgoing.WriteAsync(
            new WorkerMessage { JobResult = result },
            cancellationToken);
    }

    private async Task SendHeartbeatsAsync(
        ChannelWriter<WorkerMessage> outgoing,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(HeartbeatInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await outgoing.WriteAsync(
                CreateHeartbeatMessage(),
                cancellationToken);
        }
    }

    private static async Task WriteOutgoingAsync(
        IClientStreamWriter<WorkerMessage> requestStream,
        ChannelReader<WorkerMessage> outgoing,
        CancellationToken cancellationToken)
    {
        await foreach (var message in outgoing.ReadAllAsync(cancellationToken))
        {
            await requestStream.WriteAsync(message);
        }

        await requestStream.CompleteAsync();
    }

    private WorkerMessage CreateRegistrationMessage()
    {
        var registration = new WorkerRegistration
        {
            WorkerId = _workerOptions.Id,
            WorkerName = _workerOptions.Name,
            Model = _ollamaOptions.Model
        };
        registration.Capabilities.Add("chat");
        registration.Capabilities.Add("ollama");

        return new WorkerMessage { Registration = registration };
    }

    private WorkerMessage CreateHeartbeatMessage()
    {
        return new WorkerMessage
        {
            Heartbeat = new WorkerHeartbeat
            {
                WorkerId = _workerOptions.Id,
                UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch (RpcException exception)
            when (exception.StatusCode == StatusCode.Cancelled)
        {
        }
    }
}
