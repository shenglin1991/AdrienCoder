using AdrienCoder.Server.Infrastructure;
using AdrienCoder.Server.Features.Workers;
using AdrienCoder.Shared.Authentication;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

builder.Services.Configure<ApiKeyOptions>(
    builder.Configuration.GetSection(ApiKeyOptions.SectionName));
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        var publicBaseUrl = builder.Configuration["PublicBaseUrl"];

        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            document.Servers =
            [
                new()
            {
                Url = publicBaseUrl
            }
            ];
        }

        return Task.CompletedTask;
    });
});
builder.Services.AddControllers();
builder.Services.AddGrpc(options =>
{
    options.MaxReceiveMessageSize = 16 * 1024 * 1024;
    options.MaxSendMessageSize = 16 * 1024 * 1024;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    const string apiKeyScheme = "ApiKey";

    options.AddSecurityDefinition(
        apiKeyScheme,
        new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            Name = ApiKeyOptions.HeaderName,
            In = ParameterLocation.Header,
            Description = "API key configured on AdrienCoder.Server."
        });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(apiKeyScheme, document, null)] = []
    });
});

builder.Services
    .AddLlmFeature(builder.Configuration)
    .AddVectorFeature(builder.Configuration)
    .AddIndexingFeature()
    .AddAskFeature()
    .AddMonitoringFeature();

var app = builder.Build();

app.MapOpenApi();
app.UseSwagger();

var swaggerPrefix = app.Configuration["Swagger:Prefix"] ?? "";

app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.SwaggerEndpoint(
        $"{swaggerPrefix}/swagger/v1/swagger.json",
        "AdrienCoder API v1");
});

app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.MapControllers();
app.MapGrpcService<WorkerGatewayService>();

app.Run();
