# OPC UA for AI

This repository contains demo code and resources from the OPC UA for AI working group of the OPC Foundation. It provides tools and pre-processed data for building AI-powered applications that leverage OPC UA specifications.

## Repository Contents

### Specifications (`specifications/`)

Contains the official OPC UA specification source files and pre-generated inputs suitable for use with AI models:

- **XML Source Files** - Official OPC UA specification documents in XML format
- **Markdown Exports** (`README.md`) - Human-readable versions of each specification
- **Image Descriptions** (`image-descriptions.json`) - AI-generated text descriptions of specification diagrams
- **RAG Chunks** (`rag-chunks.json`) - Token-optimized document segments ready for vector embedding

#### Available Specifications

| Part | Title | README |
|------|-------|--------|
| Part 1 | Overview and Concepts | [View](specifications/Core/Part1/README.md) |
| Part 4 | Services | [View](specifications/Core/Part4/README.md) |
| Part 6 | Mappings | [View](specifications/Core/Part6/README.md) |
| Part 7 | Profiles | [View](specifications/Core/Part7/README.md) |
| Part 8 | DataAccess | [View](specifications/Core/Part8/README.md) |
| Part 9 | Alarms and Conditions | [View](specifications/Core/Part9/README.md) |
| Part 11 | Historical Access | [View](specifications/Core/Part11/README.md) |
| Part 13 | Aggregates | [View](specifications/Core/Part13/README.md) |
| Part 16 | State Machines | [View](specifications/Core/Part16/README.md) |
| Part 17 | Alias Names | [View](specifications/Core/Part17/README.md) |
| Part 18 | Role-Based Security | [View](specifications/Core/Part18/README.md) |
| Part 19 | Dictionary Reference | [View](specifications/Core/Part19/README.md) |
| Part 20 | File Transfer | [View](specifications/Core/Part20/README.md) |
| Part 21 | Device Onboarding | [View](specifications/Core/Onboarding/README.md) |
| Part 22 | Base Network Model | [View](specifications/Core/Part22/README.md) |
| Part 23 | Common ReferenceTypes | [View](specifications/Core/Part23/README.md) |

### Vector Database (`db/`)

A pre-populated [Qdrant](https://qdrant.tech/) vector database containing embeddings of the OPC UA specifications. This enables semantic search and retrieval-augmented generation (RAG) queries against the specification content.

To start the database:
```powershell
.\start-qdrant.ps1
```

### RAG Utility (`Opc.Ua.RagUtility/`)

A .NET command-line utility for building RAG pipelines from OPC UA specification documents. See the [Opc.Ua.RagUtility README](Opc.Ua.RagUtility/README.md) for detailed usage instructions.

Features:
- XML to Markdown conversion
- AI-powered image description generation
- Token-aware document chunking
- Vector embedding and indexing
- RAG-based question answering

### Demo Code

- `HMI25-demo.py` - Demo code from Hannovermesse 2025, shown at the OPC Foundation Cloud conference
- `run-publisher.sh` / `run-subscriber.sh` - Shell scripts for running OPC UA PubSub components

## Prerequisites

### Ollama

[Ollama](https://ollama.com/) is required for running local AI models. Download and install from:

- **Download**: https://ollama.com/download
- **Documentation**: https://ollama.com/

After installation, pull the required models:
```bash
# Embedding model
ollama pull mxbai-embed-large

# Vision model for image descriptions
ollama pull llava

# LLM for query answering
ollama pull llama3
```

### Qdrant

[Qdrant](https://qdrant.tech/) vector database is required for semantic search. The `db/` directory contains a pre-populated database, or you can run Qdrant via Docker:

```bash
docker run -p 6333:6333 -v ./db:/qdrant/storage qdrant/qdrant
```

### .NET SDK

[.NET 10.0 SDK](https://dotnet.microsoft.com/download) is required to build and run the RAG utility.

## Quick Start

1. Install prerequisites (Ollama, Qdrant, .NET SDK)
2. Start Qdrant with the pre-populated database:
   ```powershell
   .\start-qdrant.ps1
   ```
3. Start Ollama:
   ```bash
   ollama serve
   ```
4. Query the specifications using the RAG utility (see [Opc.Ua.RagUtility README](Opc.Ua.RagUtility/README.md))

## Additional Documentation

- [INSTALL.md](INSTALL.md) - Detailed installation instructions for the HMI25 demo
- [RUN-HMI25-DEMO.md](RUN-HMI25-DEMO.md) - Instructions for running the Hannovermesse 2025 demo

## License

See [LICENSE](LICENSE) for license information.

## References

- [OPC Foundation](https://opcfoundation.org/)
- [OPC UA Specifications](https://opcfoundation.org/developer-tools/specifications-unified-architecture)
- [Ollama](https://ollama.com/)
- [Qdrant](https://qdrant.tech/)
