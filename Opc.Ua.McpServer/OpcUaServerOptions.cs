namespace Opc.Ua.McpServer
{
    public class OpcUaServerOptions
    {
        public string OllamaUrl { get; set; } = "http://localhost:11434";
        public string QdrantUrl { get; set; } = "http://localhost:6333";
        public string CollectionName { get; set; } = "opcua-specifications";
        public string EmbeddingModel { get; set; } = "mxbai-embed-large";
        public string QueryModel { get; set; } = "llama3";
        public int TimeoutSeconds { get; set; } = 300;
    }
}
