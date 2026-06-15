# AdrienCoder

AdrienCoder est maintenant compose de trois applications deployables
independamment:

```text
Client.Cli
  -> scanne et decoupe le depot local
  -> POST /api/index vers le VPS
  -> POST /api/chat

Server
  -> genere les embeddings et stocke les chunks dans Qdrant
  -> construit le contexte RAG
  -> envoie le prompt au WorkerGpu
  -> fallback OpenAI-compatible (Vast), puis Ollama sur le VPS

WorkerGpu
  -> ouvre une connexion gRPC duplex sortante vers le VPS
  -> recoit les jobs LLM
  -> appelle Ollama local
  -> renvoie les resultats et un heartbeat
```

Le PC gamer n'expose aucun port entrant. Le canal gRPC persistant est toujours
initie par `AdrienCoder.WorkerGpu`.

## Solution

```text
AdrienCoder.sln
src/
  AdrienCoder.Server/
  AdrienCoder.WorkerGpu/
  AdrienCoder.Client.Cli/
  AdrienCoder.Shared/
  AdrienCoder.Contracts/
```

- `AdrienCoder.Server`: API ASP.NET Core, endpoint gRPC, orchestration LLM,
  Qdrant, embeddings et authentification par cle API.
- `AdrienCoder.WorkerGpu`: Worker Service connecte au VPS et client Ollama
  local. `ILocalLlmClient` permettra d'ajouter llama.cpp plus tard.
- `AdrienCoder.Client.Cli`: scan local, exclusions, signature SHA-256,
  chunking, upload et chat.
- `AdrienCoder.Contracts`: DTO HTTP et contrat `.proto`.
- `AdrienCoder.Shared`: options et briques communes.

`AdrienCoder.Client.Desktop` reste volontairement reporte.

## Prerequis

- SDK .NET 10
- Qdrant accessible depuis le Server
- un service d'embeddings compatible Ollama accessible depuis le Server
- Ollama et un modele de chat sur le PC gamer
- facultatif: Vast ou un autre endpoint compatible OpenAI
- facultatif: Ollama sur le VPS pour le dernier fallback

## Configuration

Ne versionnez pas de vraies cles. Utilisez les variables d'environnement:

```powershell
# Server VPS
$env:Authentication__ApiKey = "..."
$env:Qdrant__Host = "127.0.0.1"
$env:OpenAICompatible__BaseUrl = "https://..."
$env:OpenAICompatible__ApiKey = "..."

# WorkerGpu
$env:Server__BaseUrl = "https://grpc.example.com"
$env:Server__ApiKey = "..."
$env:Worker__Id = "gaming-pc"
$env:Ollama__BaseUrl = "http://127.0.0.1:11434/"
$env:Ollama__Model = "qwen2.5-coder:7b"

# Client.Cli
$env:Server__BaseUrl = "https://api.example.com"
$env:Server__ApiKey = "..."
```

En production, `AdrienCoder.Server` utilise directement
`src/AdrienCoder.Server/appsettings.json`, inclus par `dotnet publish`.
L'unite systemd force `ASPNETCORE_ENVIRONMENT=Production`, donc
`appsettings.Development.json` n'est pas charge.

Pour gRPC, le reverse proxy doit accepter HTTP/2 et conserver les connexions
longues. Un sous-domaine gRPC dedie simplifie generalement la configuration.
En developpement, l'API ecoute sur `http://localhost:5148` et gRPC en HTTP/2
sur `http://localhost:5149`. Les ports VPS internes par defaut sont `5000`
pour l'API et `5001` pour gRPC.

## Lancement

```powershell
dotnet build AdrienCoder.sln -c Release

dotnet run --project src/AdrienCoder.Server
dotnet run --project src/AdrienCoder.WorkerGpu

dotnet run --project src/AdrienCoder.Client.Cli -- index C:\dev\mon-repo
dotnet run --project src/AdrienCoder.Client.Cli -- chat "Explique le flux principal"
```

## Deploiement VPS

Le workflow GitHub Actions:

1. compile la solution avec .NET 10;
2. publie `AdrienCoder.Server` en executable autonome `linux-x64`;
3. installe l'unite systemd avec l'executable `AdrienCoder.Server`;
4. redemarre le service;
5. verifie l'API, Swagger, Ollama et Qdrant directement sur le VPS;
6. verifie ensuite l'API et Swagger a travers le reverse proxy public.

Le VPS n'a pas besoin d'avoir le runtime .NET 10 installe.

Secrets GitHub requis: `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY` et
`VPS_APP_PATH`.

L'ordre des backends LLM execute par le Server est:

```text
WorkerGpu
  -> OpenAICompatible (Vast)
  -> Ollama VPS sur 127.0.0.1:11434
```

Le scanner ignore notamment `.git`, `bin`, `obj`, `node_modules`, `dist`,
`coverage`, `.angular`, `.nx` et `.vs`. Les chemins envoyes au VPS sont
relatifs au depot.

## API

| Methode | Route | Usage |
| --- | --- | --- |
| `POST` | `/api/index` | Upload d'un depot deja decoupe par le Client |
| `POST` | `/api/chat` | Question RAG sur l'index actif |
| `POST` | `/api/ask` | Question generale |
| `POST` | `/api/ask/repo` | Compatibilite avec l'ancien endpoint RAG |
| `POST` | `/api/vector/search` | Recherche semantique |
| `GET` | `/api/vector/chunks/qdrant` | Lecture paginee des chunks actifs |
| `GET` | `/api/status` | Etat Qdrant et LLM |
| `GET` | `/api/workers` | Workers GPU connectes et dernier heartbeat |
| `GET` | `/api/health` | Sante HTTP |

La cle est transmise dans l'en-tete `X-Api-Key`. Si
`Authentication:ApiKey` est vide, l'authentification est desactivee, ce qui
doit rester limite au developpement local.

Apres deploiement:

```bash
curl https://api.example.com/api/health
curl -H "X-Api-Key: votre-cle" https://api.example.com/api/status
curl -H "X-Api-Key: votre-cle" https://api.example.com/api/workers
```

Swagger est disponible sous `/swagger`. Le bouton `Authorize` permet de saisir
la valeur de `X-Api-Key`.

## Limites du MVP

- La file de jobs et le registre des workers sont en memoire. Le Server doit
  donc tourner en une seule instance.
- Un Worker traite un job a la fois.
- L'upload HTTP est monolithique avec une limite de 100 Mio. Un protocole par
  lots sera preferable pour les tres grands depots.
- L'index actif Qdrant reste global. La prochaine evolution structurante est
  un index actif par utilisateur et par depot.
