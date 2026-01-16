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
            var vector = await m_ollama.EmbedAsync(content, m_embeddingModel);

            // Ensure collection exists
            await m_qdrant.EnsureCollectionAsync(m_collectionName, vector.Length);

            // Upsert
            await m_qdrant.UpsertAsync(m_collectionName, new QdrantPoint
            {
                Id = id.ToString(),
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
