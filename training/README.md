# AdrienCoder Training

Ce dossier documente le chemin vers un fine-tuning futur. L'objectif maintenant
est de collecter des exemples propres, pas de lancer un entrainement a
l'aveugle.

## Collecte

Lancer une evaluation RAG et ecrire un fichier JSONL:

```cmd
adriencoder eval --repo AdrienCoder --out training-data\eval-adriencoder.jsonl
```

Capturer une conversation manuelle:

```cmd
adriencoder chat --repo AdrienCoder --save-training training-data\manual.jsonl "Explique l'architecture"
```

Chaque ligne contient notamment:

- `instruction`: la question utilisateur.
- `input`: le contexte RAG injecte.
- `output`: la reponse brute du modele.
- `expectedOutput`: la correction humaine a remplir avant entrainement.
- `statusAfter`: provider, modele, repo actif et worker health.
- `latencyMs`: latence mesuree par le CLI.

Avant entrainement, copier/corriger les bonnes lignes dans un dataset propre:

```bash
mkdir -p training-data/curated
cp training-data/eval-adriencoder.jsonl training-data/curated/adriencoder-sft.jsonl
```

Puis remplir `expectedOutput` pour chaque ligne retenue. Les lignes sans
`expectedOutput` doivent etre ignorees pendant l'entrainement.

## Format SFT cible

Le prompt d'entrainement peut etre construit ainsi:

```text
Instruction:
{instruction}

Contexte:
{input}

Reponse:
{expectedOutput}
```

Pour demarrer, viser 100 a 300 exemples corriges. En dessous, le prompt et le
RAG donnent souvent plus de gain qu'un fine-tuning.

## Entrainement sur Vast

Exemple QLoRA pour un modele code 7B:

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -U pip
pip install -U torch transformers datasets accelerate peft trl bitsandbytes

export BASE_MODEL="Qwen/Qwen2.5-Coder-7B-Instruct"
export DATASET_PATH="/workspace/training-data/curated/adriencoder-sft.jsonl"
export OUTPUT_DIR="/workspace/adriencoder-qwen2.5-coder-7b-lora"

python training/train_qlora.py \
  --base-model "$BASE_MODEL" \
  --dataset "$DATASET_PATH" \
  --output-dir "$OUTPUT_DIR" \
  --max-seq-length 8192 \
  --batch-size 1 \
  --gradient-accumulation-steps 8 \
  --learning-rate 2e-4 \
  --epochs 2
```

Servir ensuite le modele de base avec l'adapter LoRA dans vLLM:

```bash
vllm serve "$BASE_MODEL" \
  --host 127.0.0.1 \
  --port 18000 \
  --enable-lora \
  --lora-modules adriencoder="$OUTPUT_DIR" \
  --max-model-len 32768 \
  --gpu-memory-utilization 0.90
```

Dans AdrienCoder Server:

```bash
export OpenAICompatible__BaseUrl="http://127.0.0.1:18000/v1/"
export OpenAICompatible__Model="adriencoder"
sudo systemctl restart adriencoder
```

## Entrainement local 3060 Ti

La 3060 Ti 8 Go est utile pour des essais courts, mais pas confortable pour
beaucoup de contexte. Reduire le contexte et garder un batch de 1:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -U pip
pip install -U torch transformers datasets accelerate peft trl bitsandbytes

$env:BASE_MODEL = "Qwen/Qwen2.5-Coder-7B-Instruct"
$env:DATASET_PATH = "training-data\curated\adriencoder-sft.jsonl"
$env:OUTPUT_DIR = "training-data\models\adriencoder-qwen2.5-coder-7b-lora"

python training\train_qlora.py `
  --base-model $env:BASE_MODEL `
  --dataset $env:DATASET_PATH `
  --output-dir $env:OUTPUT_DIR `
  --max-seq-length 4096 `
  --batch-size 1 `
  --gradient-accumulation-steps 16 `
  --learning-rate 2e-4 `
  --epochs 1
```

Si la VRAM sature, reduire `--max-seq-length` a `2048` ou faire
l'entrainement sur Vast.
