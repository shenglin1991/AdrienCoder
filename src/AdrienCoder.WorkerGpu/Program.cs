using AdrienCoder.WorkerGpu;
using AdrienCoder.WorkerGpu.Configuration;
using AdrienCoder.WorkerGpu.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

builder.Services
    .AddOptions<ServerOptions>()
    .BindConfiguration(ServerOptions.SectionName)
    .Validate(
        options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "Server:BaseUrl must be an absolute URI.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ApiKey),
        "Server:ApiKey is required.")
    .ValidateOnStart();

builder.Services
    .AddOptions<WorkerOptions>()
    .BindConfiguration(WorkerOptions.SectionName)
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Id),
        "Worker:Id is required.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Name),
        "Worker:Name is required.")
    .Validate(
        options => options.ReconnectDelaySeconds > 0,
        "Worker:ReconnectDelaySeconds must be greater than zero.")
    .ValidateOnStart();

builder.Services
    .AddOptions<OllamaOptions>()
    .BindConfiguration(OllamaOptions.SectionName)
    .Validate(
        options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "Ollama:BaseUrl must be an absolute URI.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Model),
        "Ollama:Model is required.")
    .ValidateOnStart();

builder.Services.AddSingleton<ILocalLlmClient, OllamaClient>();
builder.Services.AddHostedService<GpuWorker>();

await builder.Build().RunAsync();
