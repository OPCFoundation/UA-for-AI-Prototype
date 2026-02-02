using System.ComponentModel;
using ModelContextProtocol.Server;
using Opc.Ua;
using Opc.Ua.RagCore;

namespace Opc.Ua.McpServer
{
    [McpServerToolType]
    public class OpcUaTools
    {
        private readonly OllamaClient _ollama;
        private readonly IVectorDbClient _vectorDb;
        private readonly OpcUaServerOptions _options;

        public OpcUaTools(OllamaClient ollama, IVectorDbClient vectorDb, OpcUaServerOptions options)
        {
            _ollama = ollama;
            _vectorDb = vectorDb;
            _options = options;
        }

        [McpServerTool(Name = "specificationQuery")]
        [Description("Answer a question about the OPC UA specification using RAG (Retrieval-Augmented Generation). Use this tool to get information about OPC UA concepts, services, data types, security, and other specification details.")]
        public async Task<string> SpecificationQueryAsync(
            [Description("The question to answer about the OPC UA specification")]
            string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return "Error: Question cannot be empty.";
            }

            try
            {
                // Generate embedding for the question
                float[] embedding;
                try
                {
                    embedding = await _ollama.EmbedAsync(question, _options.EmbeddingModel);
                }
                catch (HttpRequestException ex)
                {
                    return $"Error: Cannot connect to Ollama at {_options.OllamaUrl}. Make sure Ollama is running (ollama serve). Details: {ex.Message}";
                }

                // Search for relevant documents
                List<string> docs;
                try
                {
                    docs = await _vectorDb.SearchAsync(
                        _options.CollectionName,
                        embedding,
                        topK: 5);
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is Npgsql.NpgsqlException)
                {
                    return $"Error: Cannot connect to the vector database. Make sure PostgreSQL is running and the connection string is configured. Details: {ex.Message}";
                }

                if (docs.Count == 0)
                {
                    return $"No relevant information found in the OPC UA specifications. Make sure the '{_options.CollectionName}' collection is populated in the vector database.";
                }

                // Build context from retrieved documents
                var context = string.Join("\n\n---\n\n", docs);
                var prompt = $"Use the following context from the OPC UA specification to answer the question.\n\nContext:\n{context}\n\nQuestion: {question}";

                // Generate answer
                string answer;
                try
                {
                    answer = await _ollama.GenerateAsync(prompt, _options.QueryModel);
                }
                catch (HttpRequestException ex)
                {
                    return $"Error: Failed to generate response from Ollama model '{_options.QueryModel}'. Details: {ex.Message}";
                }

                return answer;
            }
            catch (Exception ex)
            {
                return $"Error querying specification: {ex.Message}";
            }
        }
    }
}
