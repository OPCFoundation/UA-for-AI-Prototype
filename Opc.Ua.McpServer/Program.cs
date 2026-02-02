using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Opc.Ua.McpServer;
using Opc.Ua.RagCore;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets(typeof(Program).Assembly, optional: true)
    .Build();

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to write to stderr (MCP uses stdout for JSON-RPC messages)
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// Configure options from appsettings.json defaults, overridden by environment variables
// llama3 is the offline model but its answers are underwhelming; use gpt-oss:120b-cloud or gpt-oss:120b for better results if you have the resources
string Env(string name) => Environment.GetEnvironmentVariable(name);
string Resolve(string envVar, string configKey, string fallback = "")
{
    var env = Env(envVar);
    if (!string.IsNullOrEmpty(env)) return env;
    var cfg = config[configKey];
    if (!string.IsNullOrEmpty(cfg)) return cfg;
    return fallback;
}

var options = new OpcUaServerOptions
{
    OllamaUrl = Resolve("OLLAMA_URL", "Ollama:Url", "http://localhost:11434"),
    ConnectionString = Resolve("PGSQL_CONNECTION_STRING", "VectorDb:ConnectionString"),
    CollectionName = Resolve("VECTORDB_COLLECTION", "VectorDb:Collection", "opcua_specifications"),
    EmbeddingModel = Resolve("EMBEDDING_MODEL", "Models:Embedding", "mxbai-embed-large"),
    QueryModel = Resolve("QUERY_MODEL", "Models:Query", "gpt-oss:120b-cloud"),
    TimeoutSeconds = int.TryParse(Env("TIMEOUT_SECONDS"), out var timeout)
        ? timeout
        : int.TryParse(config["Timeout"], out var configTimeout)
            ? configTimeout
            : 300
};

// Register services
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(sp =>
    new OllamaClient(new Uri(options.OllamaUrl), TimeSpan.FromSeconds(options.TimeoutSeconds)));
builder.Services.AddSingleton<IVectorDbClient>(sp =>
    new PgSqlClient(options.ConnectionString));

// Configure MCP server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();
