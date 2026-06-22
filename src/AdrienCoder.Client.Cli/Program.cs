using System.Buffers.Binary;
using System.Diagnostics;
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
    private const long MaxIndexedFileBytes = 1024 * 1024;

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".github",
        ".idea",
        ".vscode",
        "bin",
        "obj",
        "node_modules",
        "dist",
        "build",
        "coverage",
        ".angular",
        ".nx",
        ".vs",
        "packages",
        "artifacts"
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
        if (command is not ("index" or "chat" or "ask" or "status" or "models" or "eval"))
        {
            return UnknownCommand(args[0]);
        }

        if (command == "index" && args.Length < 2)
        {
            Console.Error.WriteLine("Usage: adriencoder index <repoPath> [repositoryName] [--force]");
            return 1;
        }

        if (command == "chat" && args.Length < 2)
        {
            Console.Error.WriteLine(
                "Usage: adriencoder chat [--repo <repositoryName>] [--no-context] [--debug-context] <question...>");
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
                "eval" => await RunEvalAsync(client, args),
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
        var indexArgs = ParseIndexArgs(args);
        var repositoryPath = Path.GetFullPath(indexArgs.RepositoryPath);
        if (!Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException($"Dépôt introuvable: {repositoryPath}");
        }

        var repositoryName = indexArgs.RepositoryName is not null
            ? indexArgs.RepositoryName.Trim()
            : new DirectoryInfo(repositoryPath).Name;

        if (string.IsNullOrWhiteSpace(repositoryName))
        {
            throw new ArgumentException("Le nom du dépôt ne peut pas être vide.");
        }

        var files = await RepositoryScanner.ReadFilesAsync(repositoryPath);
        var signature = RepositoryScanner.ComputeSignature(files);
        var chunkCount = RepositoryScanner.CountChunks(files);
        var indexedFiles = files.Count;
        if (!indexArgs.Force)
        {
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
        }
        else
        {
            Console.WriteLine(
                "Reindex force: verification de signature ignoree, embeddings recalcules.");
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
                    batch,
                    indexArgs.Force));

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
        var chatArgs = ParseChatArgs(args);
        var question = string.Join(' ', chatArgs.QuestionArgs).Trim();

        if (question.Length == 0)
        {
            throw new ArgumentException("La question ne peut pas être vide.");
        }

        if (chatArgs.DebugContext && chatArgs.NoContext)
        {
            throw new ArgumentException(
                "--debug-context cannot be used with --no-context.");
        }

        ChatContextDebugResponse? debugContext = null;
        if (chatArgs.DebugContext
            || (!chatArgs.NoContext && chatArgs.TrainingOutputPath is not null))
        {
            debugContext = await GetDebugContextAsync(
                client,
                new ChatRequest(question, chatArgs.RepositoryName));
        }

        if (chatArgs.DebugContext && debugContext is not null)
        {
            PrintDebugContext(debugContext);
        }

        var stopwatch = Stopwatch.StartNew();
        var answer = await PostStreamAsync(
            client,
            chatArgs.NoContext ? "api/chat/ask/stream" : "api/chat/stream",
            new ChatRequest(question, chatArgs.RepositoryName));
        stopwatch.Stop();

        if (chatArgs.TrainingOutputPath is not null)
        {
            var status = await TryGetStatusAsync(client);
            await TrainingDataWriter.AppendAsync(
                chatArgs.TrainingOutputPath,
                TrainingDataRecord.FromChat(
                    question,
                    answer,
                    chatArgs.RepositoryName,
                    debugContext,
                    status,
                    stopwatch.Elapsed));
            Console.WriteLine($"Training sample saved: {chatArgs.TrainingOutputPath}");
        }

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
        Console.WriteLine($"Embedder: {response.EmbeddingBackend ?? "(unknown)"} / {response.EmbeddingModel ?? "(none)"}");
        Console.WriteLine($"LLM:      {response.Llm}");
        Console.WriteLine($"Provider: {response.ActiveProvider}");
        Console.WriteLine($"Model:    {response.Model ?? "(none)"}");
        Console.WriteLine($"Workers:  {response.WorkersHealthy}/{response.WorkersConnected} healthy");
        Console.WriteLine($"Worker:   {response.WorkerModel ?? "(none)"}");
        Console.WriteLine($"Repo:     {response.ActiveRepository ?? "(none)"}");
        Console.WriteLine($"Chunks:   {response.ActiveRepositoryChunks?.ToString() ?? "(none)"}");
        Console.WriteLine($"Index:    {FormatSignature(response.LastIndexSignature)}");
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

    private static async Task<int> RunEvalAsync(HttpClient client, string[] args)
    {
        var evalArgs = ParseEvalArgs(args);
        var questions = EvaluationQuestions.Default;
        var outputPath = evalArgs.OutputPath ?? CreateDefaultTrainingPath("eval");

        Console.WriteLine($"Eval questions: {questions.Length}");
        Console.WriteLine($"Output: {outputPath}");

        for (var index = 0; index < questions.Length; index++)
        {
            var question = questions[index];
            Console.WriteLine();
            Console.WriteLine($"[{index + 1}/{questions.Length}] {question}");

            ChatContextDebugResponse? debugContext = null;
            if (!evalArgs.NoContext)
            {
                debugContext = await GetDebugContextAsync(
                    client,
                    new ChatRequest(question, evalArgs.RepositoryName));
            }

            var beforeStatus = await TryGetStatusAsync(client);
            var stopwatch = Stopwatch.StartNew();
            var answer = await PostStreamAsync(
                client,
                evalArgs.NoContext ? "api/chat/ask/stream" : "api/chat/stream",
                new ChatRequest(question, evalArgs.RepositoryName));
            stopwatch.Stop();
            var afterStatus = await TryGetStatusAsync(client);

            await TrainingDataWriter.AppendAsync(
                outputPath,
                TrainingDataRecord.FromEval(
                    question,
                    answer,
                    evalArgs.RepositoryName,
                    debugContext,
                    beforeStatus,
                    afterStatus,
                    stopwatch.Elapsed));

            Console.WriteLine(
                $"Latency: {stopwatch.Elapsed.TotalSeconds:0.0}s | Provider: {afterStatus?.ActiveProvider ?? "(unknown)"}");
        }

        Console.WriteLine();
        Console.WriteLine($"Eval saved: {outputPath}");
        Console.WriteLine("Corrige les champs expected_output avant un fine-tuning.");
        return 0;
    }

    private static async Task<ChatContextDebugResponse> GetDebugContextAsync(
        HttpClient client,
        ChatRequest request)
    {
        return await PostAsync<ChatRequest, ChatContextDebugResponse>(
            client,
            "api/chat/context",
            request);
    }

    private static void PrintDebugContext(ChatContextDebugResponse response)
    {
        Console.WriteLine("=== RAG context ===");
        Console.WriteLine(
            $"Repository: {response.RepositoryName} ({FormatSignature(response.RepositorySignature)})");
        Console.WriteLine($"Chunks:     {response.Chunks.Count}");

        foreach (var chunk in response.Chunks)
        {
            Console.WriteLine(
                $"[{chunk.Score:0.000}] {chunk.FilePath}#{chunk.ChunkIndex}");
            Console.WriteLine(TrimForDisplay(chunk.Content, 360));
            Console.WriteLine();
        }

        Console.WriteLine("=== Answer ===");
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

    private static async Task<CliStatusResponse?> TryGetStatusAsync(HttpClient client)
    {
        try
        {
            return await GetAsync<CliStatusResponse>(client, "api/status");
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> PostStreamAsync<TRequest>(
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
        var answer = new StringBuilder();

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
                answer.Append(chunk.Delta);
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

        return answer.ToString();
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
              index <repoPath> [repositoryName] [--force]
                                                  Indexe un depot local.
              chat [--repo <repositoryName>] [--no-context] [--debug-context] [--save-training <path>] <question...>
                                                  Pose une question avec contexte RAG.
              ask <question...>                  Pose une question sans contexte RAG.
              eval [--repo <repositoryName>] [--no-context] [--out <path>]
                                                  Lance une evaluation et ecrit un JSONL.
              status                              Affiche l'etat API, Qdrant et LLM.
              models                              Affiche les modeles du backend LLM actif.

            Configuration:
              appsettings.json: Server:BaseUrl, Server:ApiKey
              environnement:   Server__BaseUrl, Server__ApiKey
            """);
    }

    private static ChatCommandArgs ParseChatArgs(string[] args)
    {
        string? repositoryName = null;
        var noContext = false;
        var debugContext = false;
        string? trainingOutputPath = null;
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

            if (argument == "--debug-context")
            {
                debugContext = true;
                continue;
            }

            if (argument == "--save-training")
            {
                if (index + 1 >= args.Length
                    || string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    throw new ArgumentException(
                        "--save-training requires an output JSONL path.");
                }

                trainingOutputPath = args[index + 1].Trim();
                index++;
                continue;
            }

            remainingArgs.Add(argument);
        }

        return new ChatCommandArgs(
            repositoryName,
            noContext,
            debugContext,
            trainingOutputPath,
            remainingArgs);
    }

    private static EvalCommandArgs ParseEvalArgs(string[] args)
    {
        string? repositoryName = null;
        string? outputPath = null;
        var noContext = false;

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

            if (argument == "--out")
            {
                if (index + 1 >= args.Length
                    || string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    throw new ArgumentException("--out requires a JSONL path.");
                }

                outputPath = args[index + 1].Trim();
                index++;
                continue;
            }

            if (argument == "--no-context")
            {
                noContext = true;
                continue;
            }

            throw new ArgumentException($"Unknown eval option: {argument}");
        }

        return new EvalCommandArgs(repositoryName, outputPath, noContext);
    }

    private static IndexCommandArgs ParseIndexArgs(string[] args)
    {
        string? repositoryName = null;
        var force = false;
        var positional = new List<string>();

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];

            if (argument == "--force")
            {
                force = true;
                continue;
            }

            if (argument.StartsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown index option: {argument}");
            }

            positional.Add(argument);
        }

        if (positional.Count is < 1 or > 2)
        {
            throw new ArgumentException(
                "Usage: adriencoder index <repoPath> [repositoryName] [--force]");
        }

        if (positional.Count == 2)
        {
            repositoryName = positional[1];
        }

        return new IndexCommandArgs(positional[0], repositoryName, force);
    }

    private static string FormatSignature(string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return "(none)";
        }

        return signature.Length <= 12 ? signature : signature[..12];
    }

    private static string TrimForDisplay(string content, int maxLength)
    {
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "...";
    }

    private static string CreateDefaultTrainingPath(string prefix)
    {
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        return Path.Combine("training-data", $"{prefix}-{stamp}.jsonl");
    }

    private sealed record ChatCommandArgs(
        string? RepositoryName,
        bool NoContext,
        bool DebugContext,
        string? TrainingOutputPath,
        IReadOnlyList<string> QuestionArgs);

    private sealed record EvalCommandArgs(
        string? RepositoryName,
        string? OutputPath,
        bool NoContext);

    private sealed record TrainingDataRecord(
        string Type,
        string Instruction,
        string Input,
        string Output,
        string ExpectedOutput,
        string? RepositoryName,
        string? RepositorySignature,
        IReadOnlyList<TrainingContextChunk> ContextChunks,
        TrainingStatusSnapshot? StatusBefore,
        TrainingStatusSnapshot? StatusAfter,
        double LatencyMs,
        DateTimeOffset CreatedAt)
    {
        public static TrainingDataRecord FromChat(
            string question,
            string answer,
            string? repositoryName,
            ChatContextDebugResponse? context,
            CliStatusResponse? status,
            TimeSpan latency)
        {
            return new TrainingDataRecord(
                "chat",
                question,
                BuildTrainingInput(context),
                answer,
                string.Empty,
                repositoryName ?? context?.RepositoryName,
                context?.RepositorySignature,
                ToTrainingChunks(context),
                null,
                TrainingStatusSnapshot.FromStatus(status),
                latency.TotalMilliseconds,
                DateTimeOffset.UtcNow);
        }

        public static TrainingDataRecord FromEval(
            string question,
            string answer,
            string? repositoryName,
            ChatContextDebugResponse? context,
            CliStatusResponse? beforeStatus,
            CliStatusResponse? afterStatus,
            TimeSpan latency)
        {
            return new TrainingDataRecord(
                "eval",
                question,
                BuildTrainingInput(context),
                answer,
                string.Empty,
                repositoryName ?? context?.RepositoryName,
                context?.RepositorySignature,
                ToTrainingChunks(context),
                TrainingStatusSnapshot.FromStatus(beforeStatus),
                TrainingStatusSnapshot.FromStatus(afterStatus),
                latency.TotalMilliseconds,
                DateTimeOffset.UtcNow);
        }

        private static string BuildTrainingInput(ChatContextDebugResponse? context)
        {
            if (context is null || context.Chunks.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Repository: {context.RepositoryName}");
            builder.AppendLine($"Signature: {context.RepositorySignature}");
            builder.AppendLine();

            foreach (var chunk in context.Chunks)
            {
                builder.AppendLine($"--- {chunk.FilePath}#{chunk.ChunkIndex} score={chunk.Score:0.000} ---");
                builder.AppendLine(chunk.Content.Trim());
                builder.AppendLine();
            }

            return builder.ToString().Trim();
        }

        private static IReadOnlyList<TrainingContextChunk> ToTrainingChunks(
            ChatContextDebugResponse? context)
        {
            return context?.Chunks
                .Select(chunk => new TrainingContextChunk(
                    chunk.FilePath,
                    chunk.ChunkIndex,
                    chunk.Score,
                    chunk.Content))
                .ToArray()
                ?? Array.Empty<TrainingContextChunk>();
        }
    }

    private sealed record TrainingContextChunk(
        string FilePath,
        int ChunkIndex,
        float Score,
        string Content);

    private sealed record TrainingStatusSnapshot(
        string? Provider,
        string? Model,
        string? ActiveRepository,
        int? ActiveRepositoryChunks,
        int WorkersConnected,
        int WorkersHealthy)
    {
        public static TrainingStatusSnapshot? FromStatus(CliStatusResponse? status)
        {
            return status is null
                ? null
                : new TrainingStatusSnapshot(
                    status.ActiveProvider,
                    status.Model,
                    status.ActiveRepository,
                    status.ActiveRepositoryChunks,
                    status.WorkersConnected,
                    status.WorkersHealthy);
        }
    }

    private static class TrainingDataWriter
    {
        public static async Task AppendAsync(string outputPath, TrainingDataRecord record)
        {
            var fullPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = JsonSerializer.Serialize(record, JsonOptions);
            await File.AppendAllTextAsync(fullPath, line + Environment.NewLine);
        }
    }

    private static class EvaluationQuestions
    {
        public static readonly string[] Default =
        [
            "Explique l'architecture AdrienCoder en 5 points.",
            "Decris le flux d'indexation depuis le CLI jusqu'a Qdrant.",
            "Comment le serveur choisit-il entre WorkerGpu, Vast et Ollama ?",
            "Comment fonctionne le streaming entre le serveur et le CLI ?",
            "Quels fichiers et dossiers sont ignores pendant l'indexation ?",
            "Comment diagnostiquer un contexte RAG qui ne contient pas les bons fichiers ?",
            "Que se passe-t-il si Vast est indisponible pendant un chat ?",
            "Quelles variables configurent les embeddings et leur parallelisme ?",
            "Quelles sont les limites MVP encore presentes dans AdrienCoder ?",
            "Comment brancher un worker GPU local et verifier qu'il est healthy ?"
        ];
    }

    private sealed record CliStatusResponse(
        string Api,
        string Qdrant,
        string Embedding,
        string Llm,
        string ActiveProvider,
        string? Model,
        string? ActiveRepository,
        string? ActiveRepositorySignature,
        int? ActiveRepositoryChunks,
        string? LastIndexRepository,
        string? LastIndexSignature,
        int? LastIndexChunks,
        string? EmbeddingBackend,
        string? EmbeddingModel,
        int WorkersConnected,
        int WorkersHealthy,
        string? WorkerModel,
        DateTimeOffset Time);

    private sealed record IndexCommandArgs(
        string RepositoryPath,
        string? RepositoryName,
        bool Force);

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
                    if (ShouldIndexFile(file))
                    {
                        yield return file;
                    }
                }
            }
        }

        private static bool ShouldIndexFile(string filePath)
        {
            var info = new FileInfo(filePath);

            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)
                || info.Length > MaxIndexedFileBytes)
            {
                return false;
            }

            Span<byte> buffer = stackalloc byte[4096];
            using var stream = File.OpenRead(filePath);
            var bytesRead = stream.Read(buffer);

            if (bytesRead == 0)
            {
                return true;
            }

            var zeroBytes = 0;
            var controlBytes = 0;

            for (var index = 0; index < bytesRead; index++)
            {
                var value = buffer[index];

                if (value == 0)
                {
                    zeroBytes++;
                    continue;
                }

                if (value < 32
                    && value is not (9 or 10 or 12 or 13 or 27))
                {
                    controlBytes++;
                }
            }

            return zeroBytes == 0 && controlBytes <= bytesRead / 20;
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
