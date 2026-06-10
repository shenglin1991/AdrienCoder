using AdrienCoder.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
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

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
