using AdrienCoder.Api.Services;
using AdrienCoder.Api.Models;
using Microsoft.Extensions.Options;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<LLMOptions>(
    builder.Configuration.GetSection("LLM"));
builder.Services.Configure<OpenAiCompatibleOptions>(
    builder.Configuration.GetSection("OpenAICompatible"));
builder.Services.Configure<OllamaOptions>(
    builder.Configuration.GetSection("Ollama"));
builder.Services.Configure<QdrantOptions>(
    builder.Configuration.GetSection("Qdrant"));

builder.Services.AddHttpClient<QdrantService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
    client.BaseAddress = new Uri($"http://{options.Host}:{options.Port}/");
    client.Timeout = TimeSpan.FromSeconds(10);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    UseProxy = false
});

builder.Services.AddHttpClient<OllamaService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromMinutes(10);
});

builder.Services.AddHttpClient<OpenAiCompatibleService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<OpenAiCompatibleOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<LlmRouterService>();
builder.Services.AddScoped<ILLMService>(sp =>
    sp.GetRequiredService<LlmRouterService>());

builder.Services.Configure<EmbeddingOptions>(
    builder.Configuration.GetSection("Embedding"));

builder.Services.AddHttpClient<EmbeddingService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<EmbeddingOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddScoped<RepoScannerService>();
builder.Services.AddScoped<CodeChunkerService>();
builder.Services.AddScoped<RepositoryIndexingService>();
builder.Services.AddSingleton<RepoIndexStore>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
