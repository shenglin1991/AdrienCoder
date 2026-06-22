namespace AdrienCoder.Contracts.Chat;

public sealed record ChatStreamChunk(
    string Delta,
    bool Done = false,
    string? Error = null);
