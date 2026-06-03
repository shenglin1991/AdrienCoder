namespace AdrienCoder.Api.Models;

public class AskRepoRequest
{
    public string Question { get; set; } = string.Empty;
    public string RepoPath { get; set; } = string.Empty;
}