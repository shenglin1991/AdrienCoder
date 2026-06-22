using System.Buffers.Binary;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AdrienCoder.Contracts.Chat;
using AdrienCoder.Contracts.Indexing;

return await CliApplication.RunAsync(args);

internal static class CliApplication
{
    private const int ChunkSize = 1200;
    private const int ChunkOverlap = 200;
    private const int IndexUploadBatchSize = 128;

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        "bin",
        "obj",
        "node_modules",
        "dist",
        "coverage",
        ".angular",
        ".nx",
        ".vs"
    };

    private static readonly HashSet<string> IncludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".ts",
        ".html",
        ".scss",
        ".json",
        ".md",
        ".yml",
        ".yaml"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync(string[] args)
    {
        string? baseUrlOverride = null;
        if (args.Length > 0)
        {
            baseUrlOverride = args[0].ToLowerInvariant() switch
            {
                "local" => "http://127.0.0.1:5000/",
                "vps" => "https://adrien-sheng-lin.fr/adriencoder/",
                _ => null
            };

            if (baseUrlOverride is not null)
            {
                args = args[1..];
            }
        }

        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        if (command is not ("index" or "chat" or "ask" or "status" or "models"))
        {
            return UnknownCommand(args[0]);
        }

        if (command == "index" && args.Length is < 2 or > 3)
        {
            Console.Error.WriteLine("Usage: adriencoder index <repoPath> [repositoryName]");
            return 1;
        }

        if (command == "chat" && args.Length < 2)
        {
            Console.Error.WriteLine(
                "Usage: adriencoder chat [--repo <repositoryName>] [--no-context] <question...>");
            return 1;
        }

        if (command == "ask" && args.Length < 2)
        {
            Console.Error.WriteLine("Usage: adriencoder ask <question...>");
            return 1;
        }

        if (command is "status" or "models" && args.Length != 1)
        {
            Console.Error.WriteLine($"Usage: adriencoder {command}");
            return 1;
        }

        try
        {
            var settings = await ServerSettings.LoadAsync(baseUrlOverride);
            using var client = CreateHttpClient(settings);

            return command switch
            {
                "index" => await RunIndexAsync(client, args),
                "chat" => await RunChatAsync(client, args),
                "ask" => await RunAskAsync(client, args),
                "status" => await RunStatusAsync(client),
                "models" => await RunModelsAsync(client),
                _ => throw new InvalidOperationException("Commande non prise en charge.")
            };
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or DirectoryNotFoundException
            or HttpRequestException
            or InvalidOperationException
            or JsonException)
        {
            Console.Error.WriteLine($"Erreur: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> RunIndexAsync(HttpClient client, string[] args)
    {
        var repositoryPath = Path.GetFullPath(args[1]);
        if (!Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException($"Dépôt introuvable: {repositoryPath}");
        }

        var repositoryName = args.Length == 3
            ? args[2].Trim()
            : new DirectoryInfo(repositoryPath).Name;

        if (string.IsNullOrWhiteSpace(repositoryName))
        {
            throw new ArgumentException("Le nom du dépôt ne peut pas être vide.");
        }

        var files = await RepositoryScanner.ReadFilesAsync(repositoryPath);
        var signature = RepositoryScanner.ComputeSignature(files);
        var chunkCount = RepositoryScanner.CountChunks(files);
        var indexedFiles = files.Count;
        var check = await PostAsync<IndexRepositoryCheckRequest, IndexRepositoryResponse>(
            client,
            "api/index/check",
            new IndexRepositoryCheckRequest(
                repositoryName,
                signature,
                indexedFiles,
                chunkCount));

        if (!check.Updated)
        {
            Console.WriteLine(
                $"Indexation terminÃ©e: {check.IndexedFiles} fichiers, " +
                $"{check.Chunks} chunks, mise Ã  jour: {check.Updated}.");
            return 0;
        }

        var chunks = RepositoryScanner.CreateChunks(files);
        var uploadedChunks = 0;

        foreach (var batch in chunks.Chunk(IndexUploadBatchSize))
        {
            uploadedChunks += batch.Length;
            await PostAsync<IndexRepositoryBatchRequest, IndexRepositoryResponse>(
                client,
                "api/index/batch",
                new IndexRepositoryBatchRequest(
                    repositoryName,
                    signature,
                    indexedFiles,
                    chunks.Count,
                    batch));

            Console.WriteLine(
                $"Indexation: {uploadedChunks}/{chunks.Count} chunks envoyes.");
        }

        var response = await PostAsync<IndexRepositoryCommitRequest, IndexRepositoryResponse>(
            client,
            "api/index/commit",
            new IndexRepositoryCommitRequest(
                repositoryName,
                signature,
                indexedFiles,
                chunks.Count));

        Console.WriteLine(
            $"Indexation terminée: {response.IndexedFiles} fichiers, " +
            $"{response.Chunks} chunks, mise à jour: {response.Updated}.");
        return 0;
    }

    private static async Task<int> RunChatAsync(HttpClient client, string[] args)
    {
        var repositoryName = ExtractRepositoryName(
            args,
            out var questionArgs,
            out var noContext);
        var question = string.Join(' ', questionArgs).Trim();

        if (question.Length == 0)
        {
            throw new ArgumentException("La question ne peut pas être vide.");
        }

        await PostStreamAsync(
            client,
            noContext ? "api/chat/ask/stream" : "api/chat/stream",
            new ChatRequest(question, repositoryName));

        return 0;
    }

    private static async Task<int> RunAskAsync(HttpClient client, string[] args)
    {
        var question = string.Join(' ', args.Skip(1)).Trim();

        if (question.Length == 0)
        {
            throw new ArgumentException("La question ne peut pas être vide.");
        }

        await PostStreamAsync(
            client,
            "api/chat/ask/stream",
            new ChatRequest(question));

        return 0;
    }

    private static async Task<int> RunStatusAsync(HttpClient client)
    {
        var response = await GetAsync<CliStatusResponse>(client, "api/status");

        Console.WriteLine($"API:      {response.Api}");
        Console.WriteLine($"Qdrant:   {response.Qdrant}");
        Console.WriteLine($"Embedding:{response.Embedding}");
        Console.WriteLine($"LLM:      {response.Llm}");
        Console.WriteLine($"Provider: {response.ActiveProvider}");
        Console.WriteLine($"Model:    {response.Model ?? "(none)"}");
        Console.WriteLine($"Time:     {response.Time:O}");
        return 0;
    }

    private static async Task<int> RunModelsAsync(HttpClient client)
    {
        var body = await GetStringAsync(client, "api/status/models");
        try
        {
            using var document = JsonDocument.Parse(body);
            Console.WriteLine(JsonSerializer.Serialize(
                document.RootElement,
                new JsonSerializerOptions(JsonOptions)
                {
                    WriteIndented = true
                }));
        }
        catch (JsonException)
        {
            Console.WriteLine(body);
        }

        return 0;
    }

    private static async Task<TResponse> PostAsync<TRequest, TResponse>(
        HttpClient client,
        string path,
        TRequest request)
    {
        using var response = await client.PostAsJsonAsync(path, request, JsonOptions);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            var detail = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body.Trim();
            throw new HttpRequestException(
                $"Le serveur a répondu {(int)response.StatusCode} ({detail}).");
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Le serveur a renvoyé une réponse vide.");
    }

    private static async Task<TResponse> GetAsync<TResponse>(
        HttpClient client,
        string path)
    {
        using var response = await client.GetAsync(path);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            var detail = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body.Trim();
            throw new HttpRequestException(
                $"Le serveur a rÃ©pondu {(int)response.StatusCode} ({detail}).");
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Le serveur a renvoyÃ© une rÃ©ponse vide.");
    }

    private static async Task PostStreamAsync<TRequest>(
        HttpClient client,
        string path,
        TRequest request)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        using var response = await client.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            var detail = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body.Trim();
            throw new HttpRequestException(
                $"Le serveur a rÃ©pondu {(int)response.StatusCode} ({detail}).");
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        var wroteAnyContent = false;

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var chunk = JsonSerializer.Deserialize<ChatStreamChunk>(
                line,
                JsonOptions);
            if (chunk is null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(chunk.Error))
            {
                throw new HttpRequestException(chunk.Error);
            }

            if (!string.IsNullOrEmpty(chunk.Delta))
            {
                Console.Write(chunk.Delta);
                wroteAnyContent = true;
            }

            if (chunk.Done)
            {
                break;
            }
        }

        if (wroteAnyContent)
        {
            Console.WriteLine();
        }
    }

    private static async Task<string> GetStringAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            var detail = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body.Trim();
            throw new HttpRequestException(
                $"Le serveur a rÃ©pondu {(int)response.StatusCode} ({detail}).");
        }

        return await response.Content.ReadAsStringAsync();
    }

    private static HttpClient CreateHttpClient(ServerSettings settings)
    {
        var client = new HttpClient
        {
            BaseAddress = settings.BaseUrl,
            Timeout = TimeSpan.FromMinutes(30)
        };

        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", settings.ApiKey);
        }

        return client;
    }

    private static bool IsHelp(string value) =>
        value is "-h" or "--help" or "help";

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Commande inconnue: {command}");
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            AdrienCoder CLI

            Profils:
              par defaut                          Server public sous /adriencoder/.
              local                               Server local sur le port 5000.
              vps                                 Alias explicite du Server public.

            Commandes:
              index <repoPath> [repositoryName]  Indexe un dépôt local.
              chat [--repo <repositoryName>] [--no-context] <question...>
                                                  Pose une question avec contexte RAG.
              ask <question...>                  Pose une question sans contexte RAG.
              status                              Affiche l'etat API, Qdrant et LLM.
              models                              Affiche les modeles du backend LLM actif.

            Configuration:
              appsettings.json: Server:BaseUrl, Server:ApiKey
              environnement:   Server__BaseUrl, Server__ApiKey
            """);
    }

    private static string? ExtractRepositoryName(
        string[] args,
        out IReadOnlyList<string> questionArgs,
        out bool noContext)
    {
        string? repositoryName = null;
        noContext = false;
        var remainingArgs = new List<string>();

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];

            if (argument is "--repo" or "-r")
            {
                if (index + 1 >= args.Length
                    || string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    throw new ArgumentException(
                        "--repo requires a repository name.");
                }

                repositoryName = args[index + 1].Trim();
                index++;
                continue;
            }

            if (argument == "--no-context")
            {
                noContext = true;
                continue;
            }

            remainingArgs.Add(argument);
        }

        questionArgs = remainingArgs;
        return repositoryName;
    }

    private sealed record CliStatusResponse(
        string Api,
        string Qdrant,
        string Embedding,
        string Llm,
        string ActiveProvider,
        string? Model,
        DateTimeOffset Time);

    private sealed record ServerSettings(Uri BaseUrl, string ApiKey)
    {
        public static async Task<ServerSettings> LoadAsync(string? baseUrlOverride = null)
        {
            string? baseUrl = null;
            string? apiKey = null;
            var settingsPath = FindSettingsPath();

            if (settingsPath is not null)
            {
                await using var stream = File.OpenRead(settingsPath);
                using var document = await JsonDocument.ParseAsync(stream);
                if (document.RootElement.TryGetProperty("Server", out var server))
                {
                    baseUrl = GetString(server, "BaseUrl");
                    apiKey = GetString(server, "ApiKey");
                }
            }

            baseUrl = baseUrlOverride
                ?? GetEnvironmentValue("Server__BaseUrl", "Server:BaseUrl")
                ?? baseUrl;
            apiKey = GetEnvironmentValue("Server__ApiKey", "Server:ApiKey") ?? apiKey;

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    "Server:BaseUrl doit être une URL HTTP(S) absolue.");
            }

            var normalizedBaseUrl = new UriBuilder(uri)
            {
                Path = $"{uri.AbsolutePath.TrimEnd('/')}/"
            }.Uri;

            return new ServerSettings(normalizedBaseUrl, apiKey ?? string.Empty);
        }

        private static string? FindSettingsPath()
        {
            var currentDirectoryPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "appsettings.json");
            if (File.Exists(currentDirectoryPath))
            {
                return currentDirectoryPath;
            }

            var applicationPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            return File.Exists(applicationPath) ? applicationPath : null;
        }

        private static string? GetString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String
                    ? property.GetString()
                    : null;

        private static string? GetEnvironmentValue(params string[] names)
        {
            foreach (var name in names)
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (value is not null)
                {
                    return value;
                }
            }

            return null;
        }
    }

    private static class RepositoryScanner
    {
        public static async Task<IReadOnlyList<RepositoryFile>> ReadFilesAsync(string rootPath)
        {
            var filePaths = EnumerateSourceFiles(rootPath)
                .Select(path => new
                {
                    AbsolutePath = path,
                    RelativePath = Path.GetRelativePath(rootPath, path).Replace('\\', '/')
                })
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .ToArray();

            var files = new List<RepositoryFile>(filePaths.Length);
            foreach (var file in filePaths)
            {
                var content = await File.ReadAllTextAsync(file.AbsolutePath);
                files.Add(new RepositoryFile(file.RelativePath, content));
            }

            return files;
        }

        public static string ComputeSignature(IReadOnlyList<RepositoryFile> files)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            foreach (var file in files)
            {
                AppendString(hash, file.RelativePath);
                AppendString(hash, file.Content);
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        public static IReadOnlyList<CodeChunkDto> CreateChunks(
            IReadOnlyList<RepositoryFile> files)
        {
            var chunks = new List<CodeChunkDto>();

            foreach (var file in files)
            {
                var chunkIndex = 0;
                foreach (var content in SplitContent(file.Content))
                {
                    chunks.Add(new CodeChunkDto(
                        CreateChunkId(file.RelativePath, chunkIndex),
                        file.RelativePath,
                        content,
                        chunkIndex));
                    chunkIndex++;
                }
            }

            return chunks;
        }

        public static int CountChunks(IReadOnlyList<RepositoryFile> files)
        {
            var chunkCount = 0;

            foreach (var file in files)
            {
                chunkCount += CountFileChunks(file.Content);
            }

            return chunkCount;
        }

        private static IEnumerable<string> EnumerateSourceFiles(string rootPath)
        {
            var directories = new Stack<string>();
            directories.Push(rootPath);

            while (directories.TryPop(out var directory))
            {
                foreach (var childDirectory in Directory.EnumerateDirectories(directory)
                    .OrderByDescending(path => path, StringComparer.Ordinal))
                {
                    var info = new DirectoryInfo(childDirectory);
                    if (!IgnoredDirectories.Contains(info.Name)
                        && !info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        directories.Push(childDirectory);
                    }
                }

                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    if (IncludedExtensions.Contains(Path.GetExtension(file)))
                    {
                        yield return file;
                    }
                }
            }
        }

        private static IEnumerable<string> SplitContent(string content)
        {
            if (content.Length == 0)
            {
                yield return string.Empty;
                yield break;
            }

            var step = ChunkSize - ChunkOverlap;
            for (var offset = 0; offset < content.Length; offset += step)
            {
                var length = Math.Min(ChunkSize, content.Length - offset);
                yield return content.Substring(offset, length);
                if (offset + length >= content.Length)
                {
                    yield break;
                }
            }
        }

        private static int CountFileChunks(string content)
        {
            if (content.Length == 0)
            {
                return 1;
            }

            var step = ChunkSize - ChunkOverlap;
            return ((content.Length - 1) / step) + 1;
        }

        private static string CreateChunkId(string relativePath, int chunkIndex)
        {
            var value = Encoding.UTF8.GetBytes($"{relativePath}\0{chunkIndex}");
            return Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
        }

        private static void AppendString(IncrementalHash hash, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            Span<byte> length = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
    }

    private sealed record RepositoryFile(string RelativePath, string Content);
}
