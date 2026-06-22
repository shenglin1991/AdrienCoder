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
$env:Embedding__ApiFormat = "Ollama"
$env:Embedding__BaseUrl = "http://127.0.0.1:11434/"
$env:Embedding__ApiKey = ""
$env:Embedding__Model = "nomic-embed-text"
$env:Embedding__VectorSize = "768"
$env:Embedding__MaxParallelism = "2"
$env:Embedding__UpsertBatchSize = "64"
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

L'indexation calcule les embeddings en parallele selon
`Embedding:MaxParallelism` et envoie les points Qdrant par lots selon
`Embedding:UpsertBatchSize`. Sur une machine GPU dediee, vous pouvez essayer
`Embedding__MaxParallelism=4`; si Ollama ralentit ou sature, revenez a `2`.
En production, les embeddings peuvent viser Vast via un endpoint
OpenAI-compatible (`Embedding:ApiFormat=OpenAICompatible`,
`Embedding:BaseUrl=http://127.0.0.1:18000/v1/`) seulement si le pod Vast sert
un endpoint `/v1/embeddings`. Le tunnel Vast de chat ne suffit pas. Par defaut,
le Server garde donc les embeddings sur Ollama VPS (`http://127.0.0.1:11434/`)
pour que le chat RAG reste disponible meme sans Vast embeddings.
La longueur de sortie LLM se regle avec `LLM:MaxOutputTokens` cote Server et
`Ollama:NumPredict` cote WorkerGpu.
Avant l'upload complet, le Client envoie une verification legere basee sur la
signature du depot: si rien n'a change, le Server reactive simplement l'index
existant. Lors d'une reindexation partielle, les chunks deja presents avec le
meme `contentHash` reutilisent leur vecteur Qdrant au lieu de recalculer
l'embedding.

Pour gRPC, le reverse proxy doit accepter HTTP/2 et conserver les connexions
longues. Un sous-domaine gRPC dedie simplifie generalement la configuration.
En developpement, l'API ecoute sur `http://localhost:5148` et gRPC en HTTP/2
sur `http://localhost:5149`. Les ports VPS internes par defaut sont `5000`
pour l'API et `5001` pour gRPC.

## Lancement

Sous Windows avec `cmd` ou Cmder, utilisez le lanceur racine:

```cmd
adriencoder build
adriencoder server
adriencoder worker
adriencoder index . AdrienCoder
adriencoder chat --repo AdrienCoder "Explique l'architecture du projet"
adriencoder ask "Reponds juste ok"
adriencoder status
adriencoder models
adriencoder local index . AdrienCoder
adriencoder local chat --repo AdrienCoder "Explique l'architecture du projet"
```

Le script `adriencoder.cmd` peut etre appele directement depuis la racine du
depot. Lancez `adriencoder build` apres chaque modification du code; les autres
commandes executent ensuite directement les DLL Release, sans recompilation.
Sans profil, le CLI cible `https://adrien-sheng-lin.fr/adriencoder/`. Le profil
`local` force `http://127.0.0.1:5000`. Le profil `vps` reste disponible comme
alias explicite. La variable `Server__ApiKey` reste utilisee dans tous les cas
lorsqu'une cle API est configuree.
Les commandes .NET completes restent disponibles:

Le Server local lance par ce script utilise la configuration `Local`: il
ecoute sur `http://127.0.0.1:5000` et expose Swagger sans prefixe VPS:
`http://127.0.0.1:5000/swagger/index.html`. Pour le Client CLI sous `cmd`/Cmder:

```cmd
set Server__BaseUrl=http://127.0.0.1:5000
```

```powershell
dotnet build AdrienCoder.sln -c Release

dotnet run --project src/AdrienCoder.Server
dotnet run --project src/AdrienCoder.WorkerGpu

dotnet run --project src/AdrienCoder.Client.Cli -- index C:\dev\mon-repo
dotnet run --project src/AdrienCoder.Client.Cli -- chat --repo mon-repo "Explique le flux principal"
dotnet run --project src/AdrienCoder.Client.Cli -- ask "Reponds juste ok"
dotnet run --project src/AdrienCoder.Client.Cli -- status
dotnet run --project src/AdrienCoder.Client.Cli -- models
```

## Deploiement VPS

Le workflow GitHub Actions:

1. compile la solution avec .NET 10;
2. publie uniquement `AdrienCoder.Server`;
3. installe l'unite systemd avec `AdrienCoder.Server.dll`;
4. redemarre le service;
5. verifie l'API, Ollama et Qdrant directement sur le VPS.

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
| `POST` | `/api/index/check` | Verification legere avant upload complet |
| `POST` | `/api/index` | Upload d'un depot deja decoupe par le Client |
| `GET` | `/api/index/status` | Index Qdrant actif |
| `GET` | `/api/index/chunks` | Consultation paginee des chunks actifs |
| `POST` | `/api/chat` | Question RAG sur l'index actif ou le `repositoryName` demande |
| `POST` | `/api/chat/ask` | Question sans contexte RAG |
| `GET` | `/api/status` | Etat Qdrant et LLM |
| `GET` | `/api/status/models` | Modeles du backend LLM actif |
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

Derriere le reverse proxy de production, le document Swagger utilise
`PublicBaseUrl`. Avec la configuration actuelle, les appels `Try it out`
partent donc vers `https://adrien-sheng-lin.fr/adriencoder/api/...`.

## Limites du MVP

- La file de jobs et le registre des workers sont en memoire. Le Server doit
  donc tourner en une seule instance.
- Un Worker traite un job a la fois.
- L'upload HTTP des chunks reste monolithique avec une limite de 100 Mio quand
  le depot a change. Un protocole par lots sera preferable pour les tres grands
  depots.
- `ask` et `chat --no-context` ne font pas de recherche RAG. Sans
  `repositoryName`, `chat` utilise encore l'index actif global. Avec
  `repositoryName`, il cible l'index nomme.
