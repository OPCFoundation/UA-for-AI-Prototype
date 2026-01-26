namespace Opc.Ua.RagUtility
{
    public class RagService : IDisposable
    {
        private readonly OllamaClient m_ollama;
        private readonly QdrantLocalClient m_qdrant;
        private readonly string m_collectionName;
        private readonly string m_embeddingModel;
        private readonly string m_queryModel;
        private bool m_disposed;

        public RagService(
            OllamaClient ollama, 
            QdrantLocalClient qdrant, 
            string collectionName,
            string embeddingModel,
            string queryModel = null)
        {
            m_ollama = ollama;
            m_qdrant = qdrant;
            m_collectionName = collectionName;
            m_embeddingModel = embeddingModel;
            m_queryModel = queryModel;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                if (disposing)
                {
                    if (m_ollama != null)
                    {
                        m_ollama.Dispose();
                    }

                    if (m_qdrant != null)
                    {
                        m_qdrant.Dispose();
                    }
                }

                m_disposed = true;
            }
        }

        public async Task IndexDocumentAsync(object id, string content)
        {
            try
            {
                await EmbedAndStoreAsync(id.ToString(), content);
            }
            catch (EmbeddingServerException ex) when (ex.StatusCode == 500)
            {
                // Split the chunk and retry
                const string headerTerminator = "---\n";
                var terminatorIndex = content.IndexOf(headerTerminator);

                if (terminatorIndex < 0)
                {
                    throw new InvalidOperationException($"Chunk {id} failed with HTTP 500 and cannot be split: no header terminator found.");
                }

                var header = content.Substring(0, terminatorIndex + headerTerminator.Length);
                var body = content.Substring(terminatorIndex + headerTerminator.Length);

                // Split body in half with 100 char overlap
                var midpoint = body.Length / 2;
                var overlapStart = Math.Max(0, midpoint - 100);

                var firstBody = body.Substring(0, midpoint);
                var secondBody = body.Substring(overlapStart);

                var firstContent = header + firstBody;
                var secondContent = header + secondBody;

                string secondId;

                if (Guid.TryParse(id.ToString(), out Guid guid))
                {
                    var bytes = guid.ToByteArray();

                    for (int ii = 1; ii < bytes.Length; ii++)
                    {
                        bytes[ii] ^= bytes[ii-1];
                    }

                    secondId = new Guid(bytes).ToString();
                }
                else
                {
                    secondId = Guid.NewGuid().ToString();
                }

                try
                {
                    await EmbedAndStoreAsync(id.ToString(), firstContent);
                    await EmbedAndStoreAsync(secondId, secondContent);
                    Console.WriteLine($"WARNING: Chunk {id} was split into two chunks ({id} and {secondId}).");
                }
                catch (EmbeddingServerException)
                {
                    throw new InvalidOperationException($"Chunk {id} failed embedding after split. Original error: {ex.Message}");
                }
            }
        }

        private async Task EmbedAndStoreAsync(string id, string content)
        {
            var vector = await m_ollama.EmbedAsync(content, m_embeddingModel);

            // Ensure collection exists
            await m_qdrant.EnsureCollectionAsync(m_collectionName, vector.Length);

            // Upsert
            await m_qdrant.UpsertAsync(m_collectionName, new QdrantPoint
            {
                Id = id,
                Vector = vector,
                Payload = new() { { "content", content } }
            });
        }

        public async Task<string> AskAsync(string question)
        {
            // Embed query
            var embedding = await m_ollama.EmbedAsync(question, m_embeddingModel);

            // Search Qdrant
            var docs = await m_qdrant.SearchAsync(m_collectionName, embedding, topK: 5);

            var context = string.Join("\n\n", docs);
            var prompt = $"Use the following context to answer the question:\n\n{context}\n\nQuestion: {question}";

            return await m_ollama.GenerateAsync(prompt, m_queryModel);
        }
    }
}
