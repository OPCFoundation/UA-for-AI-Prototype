using Opc.Ua.McpWebApi;
using Opc.Ua.RagCore;

var builder = WebApplication.CreateBuilder(args);

// Configure options from appsettings.json defaults, overridden by environment variables
var config = builder.Configuration;

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

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/api/specification/query", async (
    SpecificationQueryRequest request,
    OllamaClient ollama,
    IVectorDbClient vectorDb,
    OpcUaServerOptions opts) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest(new { error = "Question cannot be empty." });
    }

    try
    {
        // Generate embedding for the question
        float[] embedding;
        try
        {
            embedding = await ollama.EmbedAsync(request.Question, opts.EmbeddingModel);
        }
        catch (HttpRequestException ex)
        {
            return Results.Json(new { error = $"Cannot connect to Ollama at {opts.OllamaUrl}. Make sure Ollama is running (ollama serve). Details: {ex.Message}" },
                statusCode: 502);
        }

        // Search for relevant documents
        List<string> docs;
        try
        {
            docs = await vectorDb.SearchAsync(
                opts.CollectionName,
                embedding,
                topK: 5);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is Npgsql.NpgsqlException)
        {
            return Results.Json(new { error = $"Cannot connect to the vector database. Make sure PostgreSQL is running and the connection string is configured. Details: {ex.Message}" },
                statusCode: 502);
        }

        if (docs.Count == 0)
        {
            return Results.Ok(new { answer = $"No relevant information found in the OPC UA specifications. Make sure the '{opts.CollectionName}' collection is populated in the vector database." });
        }

        // Build context from retrieved documents
        var context = string.Join("\n\n---\n\n", docs);
        var prompt = $"Use the following context from the OPC UA specification to answer the question.\n\nContext:\n{context}\n\nQuestion: {request.Question}";

        // Generate answer
        string answer;
        try
        {
            answer = await ollama.GenerateAsync(prompt, opts.QueryModel);
        }
        catch (HttpRequestException ex)
        {
            return Results.Json(new { error = $"Failed to generate response from Ollama model '{opts.QueryModel}'. Details: {ex.Message}" },
                statusCode: 502);
        }

        return Results.Ok(new { answer });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = $"Error querying specification: {ex.Message}" },
            statusCode: 500);
    }
})
.WithName("SpecificationQuery")
.WithDescription("Answer a question about the OPC UA specification using RAG");

app.Run();

record SpecificationQueryRequest(string Question);
