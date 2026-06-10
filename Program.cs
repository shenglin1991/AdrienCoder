using AdrienCoder.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        var publicBaseUrl = builder.Configuration["PublicBaseUrl"];

        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            document.Servers.Clear();

            document.Servers.Add(new()
            {
                Url = publicBaseUrl
            });
        }

        return Task.CompletedTask;
    });
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddLlmFeature(builder.Configuration)
    .AddVectorFeature(builder.Configuration)
    .AddIndexingFeature()
    .AddAskFeature()
    .AddMonitoringFeature();

var app = builder.Build();

app.MapOpenApi();

var swaggerPrefix = app.Configuration["Swagger:Prefix"] ?? "";

app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.SwaggerEndpoint($"{swaggerPrefix}/openapi/v1.json", "AdrienCoder API v1");
});

app.MapControllers();

app.Run();