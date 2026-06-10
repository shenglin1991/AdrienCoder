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
