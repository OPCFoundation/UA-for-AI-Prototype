using System.ComponentModel;
using ModelContextProtocol.Server;
using Opc.Ua;

namespace Opc.Ua.McpServer
{
    [McpServerToolType]
    public class OpcUaTools
    {
        private readonly OllamaClient _ollama;
        private readonly QdrantClient _qdrant;
        private readonly OpcUaServerOptions _options;

        public OpcUaTools(OllamaClient ollama, QdrantClient qdrant, OpcUaServerOptions options)
        {
            _ollama = ollama;
            _qdrant = qdrant;
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
                    docs = await _qdrant.SearchContentAsync(
                        _options.CollectionName,
                        embedding,
                        topK: 5);
                }
                catch (HttpRequestException ex)
                {
                    return $"Error: Cannot connect to Qdrant at {_options.QdrantUrl}. Make sure Qdrant is running (start-qdrant.ps1). Details: {ex.Message}";
                }

                if (docs.Count == 0)
                {
                    return $"No relevant information found in the OPC UA specifications. Make sure the '{_options.CollectionName}' collection is populated in Qdrant.";
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

        //[McpServerTool(Name = "nodesetQuery")]
        //[Description("Get documentation for a specific OPC UA type defined in the core specification or companion specifications. You can query by BrowseName (with or without namespace URI) or by NodeId with namespace URI.")]
        //public async Task<string> NodesetQueryAsync(
        //    [Description("The BrowseName of the type (e.g., 'BaseObjectType', 'AnalogItemType', or '2:MyCustomType' with namespace index)")]
        //    string browseName = null,
        //    [Description("The namespace URI for the BrowseName (e.g., 'http://opcfoundation.org/UA/')")]
        //    string browseNameNamespaceUri = null,
        //    [Description("The NodeId string (e.g., 'i=58', 'ns=2;i=1001', 's=MyNode')")]
        //    string nodeId = null,
        //    [Description("The namespace URI for the NodeId (required when using namespace index in NodeId)")]
        //    string nodeIdNamespaceUri = null)
        //{
        //    try
        //    {
        //        string searchQuery = BuildSearchQuery(browseName, browseNameNamespaceUri, nodeId, nodeIdNamespaceUri);

        //        if (string.IsNullOrWhiteSpace(searchQuery))
        //        {
        //            return "Error: You must provide either a BrowseName or a NodeId to query.";
        //        }

        //        // Generate embedding for the search query
        //        var embedding = await _ollama.EmbedAsync(searchQuery, _options.EmbeddingModel);

        //        // Search for relevant documents
        //        var docs = await _qdrant.SearchContentAsync(
        //            _options.CollectionName,
        //            embedding,
        //            topK: 3);

        //        if (docs.Count == 0)
        //        {
        //            return $"No documentation found for the specified type: {searchQuery}";
        //        }

        //        // Build context from retrieved documents
        //        var context = string.Join("\n\n---\n\n", docs);
        //        var prompt = $"Extract and summarize the documentation for the OPC UA type described below.\n\nSearch criteria: {searchQuery}\n\nContext:\n{context}\n\nProvide a concise description of this type, its purpose, attributes, and any relevant details from the specification.";

        //        // Generate answer
        //        var answer = await _ollama.GenerateAsync(prompt, _options.QueryModel);
        //        return answer;
        //    }
        //    catch (Exception ex)
        //    {
        //        return $"Error querying nodeset: {ex.Message}";
        //    }
        //}

        //private string BuildSearchQuery(
        //    string browseName,
        //    string browseNameNamespaceUri,
        //    string nodeId,
        //    string nodeIdNamespaceUri)
        //{
        //    var parts = new List<string>();

        //    // Handle BrowseName
        //    if (!string.IsNullOrWhiteSpace(browseName))
        //    {
        //        try
        //        {
        //            // Try to parse as a QualifiedName (handles "ns:name" format)
        //            var qn = QualifiedName.Parse(browseName);

        //            if (!string.IsNullOrWhiteSpace(browseNameNamespaceUri))
        //            {
        //                parts.Add($"BrowseName '{qn.Name}' in namespace '{browseNameNamespaceUri}'");
        //            }
        //            else if (qn.NamespaceIndex > 0)
        //            {
        //                parts.Add($"BrowseName '{qn.Name}' with namespace index {qn.NamespaceIndex}");
        //            }
        //            else
        //            {
        //                parts.Add($"BrowseName '{qn.Name}'");
        //            }
        //        }
        //        catch
        //        {
        //            // If parsing fails, use the raw string
        //            if (!string.IsNullOrWhiteSpace(browseNameNamespaceUri))
        //            {
        //                parts.Add($"BrowseName '{browseName}' in namespace '{browseNameNamespaceUri}'");
        //            }
        //            else
        //            {
        //                parts.Add($"BrowseName '{browseName}'");
        //            }
        //        }
        //    }

        //    // Handle NodeId
        //    if (!string.IsNullOrWhiteSpace(nodeId))
        //    {
        //        try
        //        {
        //            // Try to parse the NodeId
        //            var parsedNodeId = NodeId.Parse(nodeId);
        //            var nodeIdStr = parsedNodeId.ToString();

        //            if (!string.IsNullOrWhiteSpace(nodeIdNamespaceUri))
        //            {
        //                parts.Add($"NodeId '{nodeIdStr}' in namespace '{nodeIdNamespaceUri}'");
        //            }
        //            else
        //            {
        //                parts.Add($"NodeId '{nodeIdStr}'");
        //            }
        //        }
        //        catch
        //        {
        //            // If parsing fails, use the raw string
        //            if (!string.IsNullOrWhiteSpace(nodeIdNamespaceUri))
        //            {
        //                parts.Add($"NodeId '{nodeId}' in namespace '{nodeIdNamespaceUri}'");
        //            }
        //            else
        //            {
        //                parts.Add($"NodeId '{nodeId}'");
        //            }
        //        }
        //    }

        //    if (parts.Count == 0)
        //    {
        //        return null;
        //    }

        //    return $"OPC UA type definition: {string.Join(" and ", parts)}";
        //}
    }
}
