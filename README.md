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
| Part 2 | Security | [View](specifications/Core/Part2/README.md) |
| Part 3 | Address Space Model | [View](specifications/Core/Part3/README.md) |
| Part 4 | Services | [View](specifications/Core/Part4/README.md) |
| Part 5 | Address Space Model | [View](specifications/Core/Part5/README.md) |
| Part 6 | Mappings | [View](specifications/Core/Part6/README.md) |
| Part 8 | DataAccess | [View](specifications/Core/Part8/README.md) |
| Part 9 | Alarms and Conditions | [View](specifications/Core/Part9/README.md) |
| Part 10 | Programs | [View](specifications/Core/Part10/README.md) |
| Part 11 | Historical Access | [View](specifications/Core/Part11/README.md) |
| Part 12 | Discovery and Global Services | [View](specifications/GDS/v105/README.md) |
| Part 13 | Aggregates | [View](specifications/Core/Part13/README.md) |
| Part 14 | PubSub | [View](specifications/Core/Part14/README.md) |
| Part 16 | State Machines | [View](specifications/Core/Part16/README.md) |
| Part 17 | Alias Names | [View](specifications/Core/Part17/README.md) |
| Part 18 | Role-Based Security | [View](specifications/Core/Part18/README.md) |
| Part 19 | Dictionary Reference | [View](specifications/Core/Part19/README.md) |
| Part 20 | File Transfer | [View](specifications/Core/Part20/README.md) |
| Part 21 | Device Onboarding | [View](specifications/Onboarding/v105/README.md) |
| Part 22 | Base Network Model | [View](specifications/Core/Part22/README.md) |
| Part 23 | Common ReferenceTypes | [View](specifications/Core/Part23/README.md) |
| Part 24 | Scheduler | [View](specifications/Scheduler/v105/README.md) |
| Part 25 | Object Serilizations | [View](specifications/Core/Part25/README.md) |
| Part 26 | LogObject | [View](specifications/Core/Part26/README.md) |

### Vector Database

A [PostgreSQL](https://www.postgresql.org/) database with the [pgvector](https://github.com/pgvector/pgvector) extension containing embeddings of the OPC UA specifications. This enables semantic search and retrieval-augmented generation (RAG) queries against the specification content.

### RAG Core Library (`Opc.Ua.RagCore/`)

A shared .NET class library containing reusable components for vector database access and AI integration. Used by both the RAG Utility and MCP Server projects.

Features:
- Vector database abstraction (`IVectorDbClient`) with PostgreSQL/pgvector implementation
- Ollama client for embeddings and LLM generation
- RAG service orchestration

### RAG Utility (`Opc.Ua.RagUtility/`)

A .NET command-line utility for building RAG pipelines from OPC UA specification documents. See the [Opc.Ua.RagUtility README](Opc.Ua.RagUtility/README.md) for detailed usage instructions.

Features:
- XML to Markdown conversion
- AI-powered image description generation
- Token-aware document chunking
- Vector embedding and indexing
- RAG-based question answering

### MCP Server (`Opc.Ua.McpServer/`)

An MCP (Model Context Protocol) server that exposes OPC UA specification knowledge to AI assistants like Claude Code. See the [Opc.Ua.McpServer README](Opc.Ua.McpServer/README.md) for setup and Claude integration instructions.

Tools:
- `specificationQuery` - Answer questions about OPC UA specifications using RAG

### MCP Web API (`Opc.Ua.McpWebApi/`)

An ASP.NET Core REST API that exposes the same OPC UA specification RAG query functionality as the MCP Server, but over HTTP. This allows any HTTP client (web browsers, curl, custom applications) to query the specifications without requiring the MCP protocol.

Endpoint:
- `POST /api/specification/query` - Submit a question and receive a RAG-generated answer

### Chat UI (`Opc.Ua.McpChat/`)

An Electron desktop application that provides a chat interface for querying OPC UA specifications. It connects to the MCP Server via stdio and lets users ask questions in natural language, with responses rendered as formatted Markdown.

To launch the Chat UI:

```
.\start-chat-ui.ps1
```

### Demo Code

- `start-chat-ui.ps1` - Launch the Chat UI (installs npm dependencies on first run)
- `do-rag-operation.ps1` - PowerShell to run the RAG utility.
- `do-mcp-query.ps1` - PowerShell MCP client for querying specifications interactively

The 'general-queries.txt' contains a set of default queries for do-mcp-query.ps1.

```
.\do-mcp-query.ps1 -InputFile general-queries.txt
```

To query using the Web API instead of the stdio-based MCP Server, use the `-Url` option:

```
.\do-mcp-query.ps1 -Url https://localhost:49322
```

Note the 

Notes when building queries:
* Extremely general queries (i.e. what is Part 8) don't work well;
* The AI can be pendantic (i.e. asking for components of a type will return only targets of HasComponent);
* If a spec does not have much general prose (Object Serialization) then generic questions like 'How to serialize an Object?' do not work. This could be addressed with FAQs for specifications tha can be added to the model.

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

# Local LLM for query answering
ollama pull llama3

# Cloud LLM for query answering with better results
ollama pull gpt-oss:120b-cloud
```

### PostgreSQL with pgvector

[PostgreSQL](https://www.postgresql.org/) with the [pgvector](https://github.com/pgvector/pgvector) extension is required for semantic search. Install PostgreSQL and enable the pgvector extension:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

**Note:** The pgvector extension is not included with standard PostgreSQL installations and may need to be manually built and installed. Pre-built packages are available for some platforms, but if your platform is not covered you will need to compile the extension from source. See the [pgvector installation instructions](https://github.com/pgvector/pgvector#installation) for details.

### .NET SDK

[.NET 10.0 SDK](https://dotnet.microsoft.com/download) is required to build and run the RAG utility.

### Database Configuration

Before running the RagUtility, McpServer, or McpWebApi applications, you must configure the PostgreSQL connection string in each project's `appsettings.json` file. The default connection string assumes a local PostgreSQL installation but requires a valid username and password to be set.

Update the `ConnectionString` value in the `VectorDb` section of each file:

- `Opc.Ua.RagUtility/appsettings.json`
- `Opc.Ua.McpServer/appsettings.json`
- `Opc.Ua.McpWebApi/appsettings.json`

Example connection string:
```
Server=localhost;Database=opcua-rag-vectors;Port=5433;User Id=<YOUR-USER>;Password=<YOUR-PASSWORD>;Ssl Mode=Prefer;Trust Server Certificate=true;
```

**Important:** The RagUtility automatically creates the required database tables on first run, so the configured database user must have sufficient privileges to create tables and extensions (i.e. admin/superuser rights on the database).

A backup of a PGSQL database with vectors preloaded is available in [OPC Foundation Teams](https://opcfoundation.sharepoint.com/:f:/s/wg.AI/IgD8b3wdQlEyR41H78qRfhhtAeKyVrRRUk25BtkayNPMP6c?e=Iovppo). If this is not accessible the database needs to be populated with the following command:

```
.\do-rag-operation.ps1 -Operation embed
```

## Quick Start

1. Install prerequisites (Ollama, PostgreSQL with pgvector, .NET SDK)
2. Start PostgreSQL and ensure the pgvector extension is enabled
3. Configure the database connection string in each project's `appsettings.json` (see [Database Configuration](#database-configuration))
4. Start Ollama:
   ```bash
   ollama serve
   ```
5. Query the specifications using the RAG utility (see [Opc.Ua.RagUtility README](Opc.Ua.RagUtility/README.md))

## Additional Documentation

- [INSTALL.md](INSTALL.md) - Detailed installation instructions for the HMI25 demo
- [RUN-HMI25-DEMO.md](RUN-HMI25-DEMO.md) - Instructions for running the Hannovermesse 2025 demo

## License

See [LICENSE](LICENSE) for license information.

## References

- [OPC Foundation](https://opcfoundation.org/)
- [OPC UA Specifications](https://opcfoundation.org/developer-tools/specifications-unified-architecture)
- [Ollama](https://ollama.com/)
- [PostgreSQL](https://www.postgresql.org/)
- [pgvector](https://github.com/pgvector/pgvector)
