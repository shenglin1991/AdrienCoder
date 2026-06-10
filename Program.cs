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
app.MapGet("/openapi-public/v1.json", async (HttpContext context) =>
{
    var httpClient = new HttpClient();

    var localUrl = $"{context.Request.Scheme}://{context.Request.Host}/openapi/v1.json";
    var json = await httpClient.GetStringAsync(localUrl);

    var publicBaseUrl = app.Configuration["PublicBaseUrl"]
        ?? "https://adrien-sheng-lin.fr/adriencoder";

    json = System.Text.RegularExpressions.Regex.Replace(
        json,
        "\"servers\"\\s*:\\s*\\[\\s*\\{\\s*\"url\"\\s*:\\s*\"[^\"]*\"\\s*\\}\\s*\\]",
        $"\"servers\":[{{\"url\":\"{publicBaseUrl}\"}}]");

    return Results.Content(json, "application/json");
});

var swaggerPrefix = app.Configuration["Swagger:Prefix"] ?? "";

app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.SwaggerEndpoint($"{swaggerPrefix}/openapi/v1.json", "AdrienCoder API v1");
});

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
