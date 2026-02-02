# Opc.Ua.McpServer

An MCP (Model Context Protocol) server that provides AI tools for querying OPC UA specifications. This server can be used with Claude Code or other MCP-compatible clients to answer questions about OPC UA specifications using RAG (Retrieval-Augmented Generation).

## Tools

### specificationQuery

Answer questions about the OPC UA specification using RAG.

**Parameters:**
- `question` (required): The question to answer about the OPC UA specification

**Example:**
```
What is a Session in OPC UA?
```

## Prerequisites

- [Ollama](https://ollama.com/) running locally with required models
- [PostgreSQL](https://www.postgresql.org/) with [pgvector](https://github.com/pgvector/pgvector) extension populated with OPC UA specification embeddings

### Required Ollama Models

```bash
ollama pull mxbai-embed-large
ollama pull llama3
ollama pull gpt-oss:120b-cloud
```

## Configuration

The server reads defaults from `appsettings.json` and allows overrides via environment variables:

| Variable | appsettings.json Key | Default | Description |
|----------|---------------------|---------|-------------|
| `OLLAMA_URL` | `Ollama:Url` | `http://localhost:11434` | Ollama server URL |
| `PGSQL_CONNECTION_STRING` | `VectorDb:ConnectionString` | `` | PostgreSQL connection string |
| `VECTORDB_COLLECTION` | `VectorDb:Collection` | `opcua_specifications` | Vector database collection name |
| `EMBEDDING_MODEL` | `Models:Embedding` | `mxbai-embed-large` | Ollama embedding model |
| `QUERY_MODEL` | `Models:Query` | `gpt-oss:120b-cloud` | Ollama LLM model for answering |
| `TIMEOUT_SECONDS` | `Timeout` | `300` | HTTP timeout in seconds |

llama3 is the default offline model but its answers are underwhelming; use gpt-oss:120b-cloud or gpt-oss:120b for better results if you have the resources.

## Building

```bash
cd Opc.Ua.McpServer
dotnet build
```

## Integrating with Claude Code

### Step 1: Start Required Services

Before using the MCP server with Claude, ensure the backend services are running:

```powershell
# Terminal 1: Start PostgreSQL (ensure pgvector extension is enabled)

# Terminal 2: Start Ollama
ollama serve
```

### Step 2: Configure Claude Code

Add the MCP server to your Claude Code settings. You have two options:

#### Option A: Project-level Configuration (Recommended)

Create or edit `.claude/settings.json` in the repository root:

```json
{
  "mcpServers": {
    "opcua": {
      "command": "dotnet",
      "args": ["run", "--project", "Opc.Ua.McpServer", "--configuration", "Release"],
      "env": {
        "OLLAMA_URL": "http://localhost:11434",
        "PGSQL_CONNECTION_STRING": "Host=localhost;Database=opcua;Username=postgres;Password=yourpassword",
        "VECTORDB_COLLECTION": "opcua_specifications",
        "QUERY_MODEL": "gpt-oss:120b-cloud"
      }
    }
  }
}
```

#### Option B: User-level Configuration

Edit your user settings file:
- **Windows**: `%USERPROFILE%\.claude\settings.json`
- **macOS/Linux**: `~/.claude/settings.json`

```json
{
  "mcpServers": {
    "opcua": {
      "command": "dotnet",
      "args": ["run", "--project", "D:\\path\\to\\Opc.Ua.McpServer", "--configuration", "Release"],
      "env": {
        "OLLAMA_URL": "http://localhost:11434",
        "PGSQL_CONNECTION_STRING": "Host=localhost;Database=opcua;Username=postgres;Password=yourpassword",
        "VECTORDB_COLLECTION": "opcua_specifications",
        "QUERY_MODEL": "gpt-oss:120b-cloud"
      }
    }
  }
}
```

### Step 3: Restart Claude Code

After adding the configuration, restart Claude Code to load the MCP server.

### Step 4: Verify the Integration

1. Run `/mcp` in Claude Code to see connected MCP servers
2. The `opcua` server should be listed with the `specificationQuery` tool
3. Ask Claude a question about OPC UA - it will automatically use the tool when relevant

### Example Usage

Once configured, you can ask Claude questions like:

- "What is a Session in OPC UA?"
- "How does OPC UA handle security?"
- "Explain the OPC UA Address Space model"
- "What are the different node classes in OPC UA?"

Claude will use the `specificationQuery` tool to search the OPC UA specifications and provide accurate, grounded answers.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Opc.Ua.McpServer                        │
├─────────────────────────────────────────────────────────────┤
│  MCP Tools                                                  │
│  └── specificationQuery  - RAG-based Q&A                    │
├─────────────────────────────────────────────────────────────┤
│  Opc.Ua.RagCore (shared library)                            │
│  ├── OllamaClient        - Embeddings & LLM generation      │
│  ├── PgSqlClient         - PostgreSQL/pgvector search        │
│  └── IVectorDbClient     - Vector DB abstraction             │
└─────────────────────────────────────────────────────────────┘
         │                              │
         ▼                              ▼
┌─────────────────┐          ┌─────────────────┐
│     Ollama      │          │   PostgreSQL    │
│  (AI Models)    │          │  (pgvector)     │
└─────────────────┘          └─────────────────┘
```

## Troubleshooting

### MCP Server Not Connecting

1. Check that the project path in your settings is correct
2. Ensure .NET SDK is installed: `dotnet --version`
3. Try building manually: `dotnet build Opc.Ua.McpServer`

### "Cannot connect to Ollama" Error

1. Verify Ollama is running: `curl http://localhost:11434/api/tags`
2. Check the required models are installed: `ollama list`
3. Pull missing models: `ollama pull llama3`

### "Cannot connect to the vector database" Error

1. Verify PostgreSQL is running and accessible
2. Check the connection string is correctly configured via `PGSQL_CONNECTION_STRING` or `appsettings.json`
3. Ensure the pgvector extension is enabled: `CREATE EXTENSION IF NOT EXISTS vector;`
4. If the collection is missing, run the embedding process using `do-rag-operation.ps1 -Operation embed`

### No Results Found

1. Ensure the PostgreSQL database is populated with embeddings
2. Try a simpler query to verify the pipeline works
3. Check the `VECTORDB_COLLECTION` environment variable matches your collection name

## Dependencies

- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) - MCP SDK for .NET
- [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting) - .NET hosting infrastructure
- [Opc.Ua.RagCore](../Opc.Ua.RagCore/) - Shared RAG components (OllamaClient, PgSqlClient, IVectorDbClient)
