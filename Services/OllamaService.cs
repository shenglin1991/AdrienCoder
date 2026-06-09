using System.Net.Http.Json;
using System.Text.Json;

namespace AdrienCoder.Api.Services;

public class OllamaService : ILLMService
{
    private readonly HttpClient _httpClient;

    public OllamaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> AskAsync(string question)
    {
        var payload = new
        {
            model = "qwen2.5-coder:7b",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = """
                    Tu es AdrienCoder, un assistant personnel de développement.
                    Tu aides Adrien sur Angular, .NET, NestJS, Nx, tests, refactoring et architecture.
                    Réponds clairement, avec du code propre quand c'est utile.
                    """
                },
                new
                {
                    role = "user",
                    content = question
                }
            },
            stream = false
        };

        var response = await _httpClient.PostAsJsonAsync("/api/chat", payload);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return doc.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    public async Task<string> GetModelsAsync()
    {
        var response = await _httpClient.GetAsync("/api/tags");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> AskWithContextAsync(string question, string context)
    {
        var fullPrompt = $"""
                            Tu vas répondre à une question sur un projet de code.

                            Voici les fichiers du projet :

                            {context}

                            Question :
                            {question}

                            Réponds de façon structurée et concise.
                            """;

        return await AskAsync(fullPrompt);
    }
}