# AdrienCoder

AdrienCoder est une API .NET conçue comme un assistant IA personnel pour le
développement logiciel. Elle peut répondre à des questions générales ou
analyser un dépôt de code grâce à un pipeline RAG basé sur Qdrant.

## Fonctionnalités

- Interrogation d'un modèle LLM depuis une API REST.
- Utilisation prioritaire d'un serveur compatible OpenAI, par exemple Vast.ai.
- Bascule automatique vers Ollama lorsque le fournisseur principal est
  indisponible.
- Scan et découpage d'un dépôt de code en fragments.
- Génération d'embeddings avec Ollama.
- Indexation et recherche sémantique dans Qdrant.
- Ajout automatique du contexte pertinent avant l'interrogation du LLM.
- Endpoints de santé et de suivi des services.
- Documentation interactive avec Swagger.

## Architecture

Le projet est organisé par fonctionnalité. Chaque feature contient son
contrôleur, ses modèles et ses services.

```text
Features/
├── Ask/
│   ├── Models/
│   ├── Services/
│   └── AskController.cs
├── Indexing/
│   ├── Models/
│   ├── Services/
│   └── IndexController.cs
├── Llm/
│   ├── Models/
│   ├── Services/
│   └── ModelsController.cs
├── Monitoring/
│   ├── Models/
│   ├── Services/
│   ├── HealthController.cs
│   └── StatusController.cs
└── Vector/
    ├── Models/
    ├── Services/
    └── VectorController.cs

Infrastructure/
└── DependencyInjection.cs
```

## Routage LLM

`LlmRouterService` choisit automatiquement le fournisseur à utiliser :

```text
Question
   │
   ▼
LlmRouterService
   ├── OpenAICompatible disponible → Vast.ai ou serveur compatible OpenAI
   └── indisponible ou en erreur    → Ollama
```

Le fournisseur principal et le fournisseur de secours sont configurables dans
`appsettings.json`.

## Analyse d'un dépôt

L'endpoint `POST /api/ask/repo` exécute le pipeline suivant :

1. Scan des fichiers pris en charge dans le dépôt.
2. Exclusion des répertoires générés comme `bin`, `obj`, `.git` et
   `node_modules`.
3. Découpage des fichiers en fragments avec chevauchement.
4. Génération des embeddings.
5. Indexation des fragments dans Qdrant.
6. Recherche des fragments les plus proches de la question.
7. Construction du contexte envoyé au LLM.
8. Génération de la réponse finale.

Les identifiants des points Qdrant sont déterministes. Une nouvelle indexation
remplace donc les fragments existants au lieu de créer des doublons.

## Configuration

Exemple de configuration :

```json
{
    "LLM": {
        "PreferredProvider": "OpenAICompatible",
        "FallbackProvider": "Ollama",
        "SystemPrompt": "Tu es AdrienCoder, assistant de développement."
    },
    "OpenAICompatible": {
        "BaseUrl": "http://127.0.0.1:18000/v1/",
        "ApiKey": "",
        "Model": "Qwen/Qwen3-8B-FP8"
    },
    "Ollama": {
        "BaseUrl": "http://127.0.0.1:11434/",
        "Model": "qwen2.5-coder:7b"
    },
    "Qdrant": {
        "Host": "127.0.0.1",
        "Port": 6333,
        "CollectionName": "code"
    },
    "Embedding": {
        "BaseUrl": "http://127.0.0.1:11434/",
        "Model": "nomic-embed-text",
        "VectorSize": 768
    }
}
```

Les clés API et adresses privées doivent être fournies avec les variables
d'environnement ou une configuration locale non versionnée.

## Endpoints

### Ask

| Méthode | Route           | Description                                     |
| ------- | --------------- | ----------------------------------------------- |
| `POST`  | `/api/ask`      | Pose une question générale au LLM.              |
| `POST`  | `/api/ask/repo` | Analyse un dépôt puis répond avec son contexte. |

### Index

| Méthode | Route               | Description                             |
| ------- | ------------------- | --------------------------------------- |
| `POST`  | `/api/index/repo`   | Scanne et conserve un dépôt en mémoire. |
| `GET`   | `/api/index/status` | Affiche l'état de l'index en mémoire.   |

### Vector

| Méthode | Route                | Description                         |
| ------- | -------------------- | ----------------------------------- |
| `GET`   | `/api/vector/chunks` | Prévisualise les fragments générés. |
| `POST`  | `/api/vector/index`  | Indexe les fragments dans Qdrant.   |
| `POST`  | `/api/vector/search` | Effectue une recherche sémantique.  |
| `GET`   | `/api/vector/status` | Affiche l'état de Qdrant.           |

### Monitoring

| Méthode | Route         | Description                                     |
| ------- | ------------- | ----------------------------------------------- |
| `GET`   | `/api/Health` | Vérifie que l'API répond.                       |
| `GET`   | `/api/Status` | Affiche Qdrant, le LLM et le fournisseur actif. |
| `GET`   | `/api/Models` | Retourne les modèles du fournisseur LLM actif.  |

## Lancement

Prérequis :

- SDK .NET 10
- Qdrant
- Ollama avec le modèle d'embedding configuré
- Un modèle Ollama local ou un serveur compatible OpenAI

```powershell
dotnet restore
dotnet run
```

En environnement de développement, Swagger est disponible à l'adresse indiquée
dans la sortie de l'application, généralement sous `/swagger`.

## Pistes d'amélioration

- Ajouter une interface web ou desktop.
- Mettre en cache l'état des fournisseurs LLM.
- Ne réindexer que les fichiers modifiés.
- Ajouter l'authentification et la gestion des utilisateurs.
- Conserver plusieurs dépôts dans des collections Qdrant séparées.
- Ajouter des tests unitaires et des tests d'intégration.
- Prendre en charge le streaming des réponses.
  ssh -N -L 11434:localhost:11434 -L 6333:localhost:6333 ubuntu@ip_serveur-vps
