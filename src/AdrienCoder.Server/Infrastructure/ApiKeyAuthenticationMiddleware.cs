using AdrienCoder.Shared.Authentication;
using Microsoft.Extensions.Options;

namespace AdrienCoder.Server.Infrastructure;

public sealed class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public ApiKeyAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IOptions<ApiKeyOptions> options)
    {
        var configuredApiKey = options.Value.ApiKey;

        if (string.IsNullOrWhiteSpace(configuredApiKey)
            || context.Request.Path.StartsWithSegments("/api/health")
            || context.Request.Path.StartsWithSegments("/openapi")
            || context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(
                ApiKeyOptions.HeaderName,
                out var suppliedApiKey)
            || suppliedApiKey.Count != 1
            || !string.Equals(
                suppliedApiKey[0],
                configuredApiKey,
                StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "A valid API key is required."
            });
            return;
        }

        await _next(context);
    }
}
