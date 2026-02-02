namespace Opc.Ua.McpServer
{
    public class OpcUaServerOptions
    {
        public string OllamaUrl { get; set; } = "http://localhost:11434";
        public string ConnectionString { get; set; } = "";
        public string CollectionName { get; set; } = "opcua_specifications";
        public string EmbeddingModel { get; set; } = "mxbai-embed-large";
        public string QueryModel { get; set; } = "gpt-oss:120b-cloud";
        public int TimeoutSeconds { get; set; } = 300;
    }
}
