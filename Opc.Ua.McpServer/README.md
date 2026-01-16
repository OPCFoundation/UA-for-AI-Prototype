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
- [Qdrant](https://qdrant.tech/) vector database populated with OPC UA specifications

### Required Ollama Models

```bash
ollama pull mxbai-embed-large
ollama pull llama3
ollama pull gpt-oss:120b-cloud
```

## Configuration

The server is configured via environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `OLLAMA_URL` | `http://localhost:11434` | Ollama server URL |
| `QDRANT_URL` | `http://localhost:6333` | Qdrant server URL |
| `QDRANT_COLLECTION` | `opcua-specifications` | Qdrant collection name |
| `EMBEDDING_MODEL` | `mxbai-embed-large` | Ollama embedding model |
| `QUERY_MODEL` | `llama3` | Ollama LLM model for answering |
| `TIMEOUT_SECONDS` | `300` | HTTP timeout in seconds |

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
# Terminal 1: Start Qdrant (from the repository root)
.\start-qdrant.ps1

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
        "QDRANT_URL": "http://localhost:6333",
        "QDRANT_COLLECTION": "opcua-specifications",
        "QUERY_MODEL": "llama3"
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
        "QDRANT_URL": "http://localhost:6333",
        "QDRANT_COLLECTION": "opcua-specifications",
        "QUERY_MODEL": "llama3"
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
│  Services                                                   │
│  ├── OllamaClient        - Embeddings & LLM generation      │
│  └── QdrantClient        - Vector search                    │
└─────────────────────────────────────────────────────────────┘
         │                              │
         ▼                              ▼
┌─────────────────┐          ┌─────────────────┐
│     Ollama      │          │     Qdrant      │
│  (AI Models)    │          │  (Vector DB)    │
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

### "Cannot connect to Qdrant" Error

1. Verify Qdrant is running: `curl http://localhost:6333/collections`
2. Check the collection exists: `curl http://localhost:6333/collections/opcua-specifications`
3. If missing, run the embedding process using `do-rag-operation.ps1 -Operation embed`

### No Results Found

1. Ensure the Qdrant collection is populated with embeddings
2. Try a simpler query to verify the pipeline works
3. Check the `QDRANT_COLLECTION` environment variable matches your collection name

## Dependencies

- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) - MCP SDK for .NET
- [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting) - .NET hosting infrastructure
