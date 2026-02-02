# OPC.Ua.RagUtility

A .NET command-line utility for building Retrieval-Augmented Generation (RAG) pipelines from OPC UA specification documents. This tool processes official OPC UA specification XML files and prepares them for AI-powered question answering.

## Overview

OPC.Ua.RagUtility is part of the OPC Foundation's AI for IIoT initiative. It enables:

- Converting OPC UA technical specifications into AI-searchable knowledge bases
- Answering technical questions about OPC UA using AI models grounded in official specifications
- Preventing AI hallucination by using RAG (answers based only on provided context)
- Working with open-source AI models via Ollama for offline, secure processing

## Features

- **XML to Markdown Conversion** - Parse OPC UA specification XML and export as readable markdown with images
- **AI-Powered Image Description** - Generate text descriptions of specification diagrams using vision models
- **Token-Aware Document Chunking** - Split documents into optimal segments for vector embeddings
- **Vector Embedding** - Generate and store embeddings in PostgreSQL with pgvector
- **RAG Query Answering** - Answer questions using semantic search + LLM generation

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.ai/) - Local AI model server
- [PostgreSQL](https://www.postgresql.org/) with [pgvector](https://github.com/pgvector/pgvector) - Vector database

### Required Ollama Models

Pull these models before using the tool:

```bash
# Embedding model (1024-dimensional vectors)
ollama pull mxbai-embed-large

# Vision model for image descriptions
ollama pull llava

# LLM for query answering (or substitute your preferred model)
ollama pull llama3
```

## Installation

### Build from Source

```bash
cd Opc.Ua.RagUtility
dotnet restore
dotnet build
```

### Verify Installation

```bash
dotnet run -- --help
```

## Usage

The tool provides five commands that form a processing pipeline:

```
XML Input -> markdown -> describe-images -> generate-chunks -> embed -> prompt
```

### 1. Convert XML to Markdown

Parse an OPC UA specification XML file and export as markdown with images.

```bash
dotnet run -- markdown --input <xml_path> --output <output_dir>
```

**Options:**
| Option | Short | Description |

|---|---|---|
| `--input` | `-i` | Path to XML dump file (required) |
| `--output` | `-o` | Output directory for README.md and images |

**Example:**
```bash
dotnet run -- markdown -i ./specs/Part4.xml -o ./output/Part4
```

**Output:**
- `README.md` - Formatted specification content
- `images/` - Extracted specification diagrams

---

### 2. Generate Image Descriptions

Use a vision AI model to generate text descriptions for specification diagrams.

```bash
dotnet run -- describe-images --input <xml_path> --output <json_file>
```

**Options:**
| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--input` | `-i` | | Path to XML dump file (required) |
| `--agent` | `-a` | `http://localhost:11434` | Ollama server URL |
| `--model` | `-m` | `llava` | Vision model name |
| `--timeout` | `-t` | `300` | HTTP timeout in seconds |
| `--output` | `-o` | | Output JSON file path |

**Example:**
```bash
dotnet run -- describe-images -i ./specs/Part4.xml -a http://localhost:11434 -m llava -o ./output/Part4-images.json
```

---

### 3. Generate RAG Chunks

Split documents into token-optimized chunks suitable for vector embedding.

```bash
dotnet run -- generate-chunks --input <xml_path> --output <json_file>
```

**Options:**
| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--input` | `-i` | | Path to XML dump file (required) |
| `--tokens` | `-t` | `400` | Maximum tokens per chunk |
| `--images` | `-m` | | Image descriptions JSON (optional) |
| `--output` | `-o` | | Output JSON file path |

**Example:**
```bash
dotnet run -- generate-chunks -i ./specs/Part4.xml -t 400 -m ./output/Part4-images.json -o ./output/Part4-chunks.json
```

**Chunk Structure:**
- Each chunk includes a header with document title, section, and figure caption context
- Chunks overlap by 5 words to maintain continuity
- Token count uses GPT-4 tokenizer for accuracy

---

### 4. Embed and Index Chunks

Generate vector embeddings and store them in the vector database for semantic search.

```bash
dotnet run -- embed --input <chunks_json>
```

**Options:**
| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--input` | `-i` | | RAG chunks JSON file (required) |
| `--agent` | `-a` | `http://localhost:11434` | Ollama server URL |
| `--embed` | `-em` | `mxbai-embed-large` | Embedding model name |
| `--db` | `-d` | | Vector DB connection string |
| `--vectordb-type` | `-vt` | `pgsql` | Vector database type (pgsql or qdrant) |
| `--timeout` | `-t` | `300` | HTTP timeout in seconds |
| `--collection` | `-n` | `opcua_specifications` | Collection name |

**Example:**
```bash
dotnet run -- embed -i ./output/Part4-chunks.json -n opcua_specifications
```

---

### 5. Query the Knowledge Base

Ask questions and get answers grounded in the indexed specifications.

```bash
dotnet run -- prompt --input <queries_json>
```

**Options:**
| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--input` | `-i` | | Query JSON file (required) |
| `--agent` | `-a` | `http://localhost:11434` | Ollama server URL |
| `--model` | `-m` | `mxbai-embed-large` | Embedding model for queries |
| `--query` | `-qm` | `gpt-oss:120b-cloud` | LLM model for answers |
| `--db` | `-d` | | Vector DB URL (for Qdrant) |
| `--vectordb-type` | `-vt` | `pgsql` | Vector database type (pgsql or qdrant) |
| `--connection-string` | `-cs` | | PostgreSQL connection string |
| `--timeout` | `-t` | `300` | HTTP timeout in seconds |
| `--collection` | `-n` | `opcua_specifications` | Collection name |

**Query JSON Format:**
```json
{
  "queries": [
    { "question": "What is the purpose of OPC UA sessions?" },
    { "question": "How does subscription work in OPC UA?" }
  ]
}
```

**Example:**
```bash
dotnet run -- prompt -i ./queries.json -qm llama3 -n opcua_specifications
```

## Complete Workflow Example

Process a specification from XML to queryable knowledge base:

```bash
# 1. Start required services
# Terminal 1: Start Ollama
ollama serve

# Terminal 2: Ensure PostgreSQL is running with pgvector extension

# 2. Process the specification
# Convert to markdown (optional, for human review)
dotnet run -- markdown -i ./specs/Part4-Services.xml -o ./output/Part4

# Generate image descriptions
dotnet run -- describe-images -i ./specs/Part4-Services.xml -o ./output/Part4-images.json

# Create chunks
dotnet run -- generate-chunks -i ./specs/Part4-Services.xml -m ./output/Part4-images.json -o ./output/Part4-chunks.json

# Index in vector database
dotnet run -- embed -i ./output/Part4-chunks.json -n opcua_specifications

# 3. Query the knowledge base
dotnet run -- prompt -i ./my-queries.json -qm llama3 -n opcua_specifications
```

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        OPC.Ua.RagUtility                        │
├─────────────────────────────────────────────────────────────────┤
│  Program.cs                                                     │
│  └── CLI Commands (markdown, describe-images, generate-chunks,  │
│                    embed, prompt)                               │
├─────────────────────────────────────────────────────────────────┤
│  Document Processing                                            │
│  ├── DocumentImporter.cs  - XML parsing & normalization         │
│  ├── Document.cs          - Data models                         │
│  ├── DataSlicer.cs        - Token-aware chunking                │
│  └── MarkdownExporter.cs  - Markdown generation                 │
├─────────────────────────────────────────────────────────────────┤
│  Opc.Ua.RagCore (shared library)                                │
│  ├── OllamaClient.cs      - Embedding, generation, vision       │
│  ├── RagService.cs        - RAG workflow orchestration          │
│  ├── PgSqlClient.cs       - PostgreSQL/pgvector storage          │
│  └── IVectorDbClient.cs   - Vector DB abstraction               │
└─────────────────────────────────────────────────────────────────┘
         │                              │
         ▼                              ▼
┌─────────────────┐          ┌─────────────────┐
│     Ollama      │          │   PostgreSQL    │
│  (AI Models)    │          │  (pgvector)     │
│                 │          │                 │
│ - mxbai-embed   │          │ - Collections   │
│ - llava         │          │ - Cosine search │
│ - llama3        │          │                 │
└─────────────────┘          └─────────────────┘
```

## Configuration

Configuration is loaded from `appsettings.json` (provided by the Opc.Ua.RagCore library) and can be overridden via command-line options.

### Default Service URLs

| Service | Default URL |
|---------|-------------|
| Ollama | `http://localhost:11434` |
| PostgreSQL | Via connection string |

### Recommended Models

| Purpose | Model | Notes |
|---------|-------|-------|
| Embeddings | `mxbai-embed-large` | 1024-dimensional vectors |
| Vision | `llava` | Image description |
| Query LLM | `llama3` | Or any chat-capable model |

### Chunk Size Tuning

The default chunk size of 400 tokens balances:
- Sufficient context for meaningful retrieval
- Room for query embedding comparison
- Efficient vector storage

Adjust with `--tokens` based on your embedding model's context window.

## Troubleshooting

### Ollama Connection Failed

```bash
# Verify Ollama is running
curl http://localhost:11434/api/tags

# Check if required models are available
ollama list
```

### PostgreSQL Connection Failed

```bash
# Verify PostgreSQL is running and accessible
psql -h localhost -U postgres -c "SELECT 1;"

# Check pgvector extension is enabled
psql -h localhost -U postgres -d opcua -c "SELECT * FROM pg_extension WHERE extname = 'vector';"
```

### Timeout Errors

For large documents or slow hardware, increase the timeout:

```bash
dotnet run -- describe-images -i ./specs/Part4.xml -t 600 -o ./output.json
```

### Memory Issues

Process specifications individually rather than batching. Each command processes one XML file at a time.

## Dependencies

- **Microsoft.ML.Tokenizers** (v2.0.0) - GPT-4 tokenizer for accurate token counting
- **Microsoft.ML.Tokenizers.Data.Cl100kBase** (v2.0.0) - Token encoding data
- **System.CommandLine** (v2.0.2) - CLI argument parsing
- **Opc.Ua.RagCore** - Shared RAG components (OllamaClient, PgSqlClient, RagService)

## References

- [OPC Foundation](https://opcfoundation.org/)
- [OPC UA Specifications](https://opcfoundation.org/developer-tools/specifications-unified-architecture)
- [Ollama Documentation](https://ollama.ai/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [pgvector Documentation](https://github.com/pgvector/pgvector)

## License

See the parent project for license information.
