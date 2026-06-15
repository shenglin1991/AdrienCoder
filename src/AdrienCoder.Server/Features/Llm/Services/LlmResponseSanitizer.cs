using System.Text.RegularExpressions;

namespace AdrienCoder.Server.Features.Llm.Services;

internal static partial class LlmResponseSanitizer
{
    public static string RemoveThinking(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return string.Empty;
        }

        var sanitizedResponse = CompleteThinkBlockRegex().Replace(response, "");

        // Some models stop before emitting </think>. In that case, hide everything
        // after the opening tag rather than exposing the unfinished reasoning.
        sanitizedResponse = UnclosedThinkBlockRegex().Replace(
            sanitizedResponse,
            "");

        return ThinkTagRegex()
            .Replace(sanitizedResponse, "")
            .Trim();
    }

    [GeneratedRegex(
        @"<think\b[^>]*>.*?</think\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CompleteThinkBlockRegex();

    [GeneratedRegex(
        @"<think\b[^>]*>.*$",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex UnclosedThinkBlockRegex();

    [GeneratedRegex(
        @"</?think\b[^>]*>",
        RegexOptions.IgnoreCase)]
    private static partial Regex ThinkTagRegex();
}
