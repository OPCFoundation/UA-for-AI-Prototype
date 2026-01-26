using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Opc.Ua.McpServer;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to write to stderr (MCP uses stdout for JSON-RPC messages)
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// Configure options from environment variables or defaults
// llama3 is the offline model but its answers are underwhelming; use gpt-oss:120b-cloud or gpt-oss:120b for better results if you have the resources
var options = new OpcUaServerOptions
{
    OllamaUrl = Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://localhost:11434",
    QdrantUrl = Environment.GetEnvironmentVariable("QDRANT_URL") ?? "http://localhost:6333",
    CollectionName = Environment.GetEnvironmentVariable("QDRANT_COLLECTION") ?? "opcua-specifications",
    EmbeddingModel = Environment.GetEnvironmentVariable("EMBEDDING_MODEL") ?? "mxbai-embed-large",
    QueryModel = Environment.GetEnvironmentVariable("QUERY_MODEL") ?? "gpt-oss:120b-cloud", // "llama3",
    TimeoutSeconds = int.TryParse(Environment.GetEnvironmentVariable("TIMEOUT_SECONDS"), out var timeout) ? timeout : 300
};

// Register services
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(sp =>
    new OllamaClient(options.OllamaUrl, TimeSpan.FromSeconds(options.TimeoutSeconds)));
builder.Services.AddSingleton(sp =>
    new QdrantClient(options.QdrantUrl, TimeSpan.FromSeconds(options.TimeoutSeconds)));

// Configure MCP server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();
