namespace AdrienCoder.Contracts.Chat;

public sealed record ChatRequest(
    string Question,
    string? RepositoryName = null);
