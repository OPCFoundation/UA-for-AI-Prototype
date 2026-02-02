namespace Opc.Ua.RagCore
{
    public class RagService : IDisposable
    {
        private readonly OllamaClient m_ollama;
        private readonly IVectorDbClient m_vectorDb;
        private readonly string m_collectionName;
        private readonly string m_embeddingModel;
        private readonly string m_queryModel;
        private bool m_disposed;

        public RagService(
            OllamaClient ollama,
            IVectorDbClient vectorDb,
            string collectionName,
            string embeddingModel,
            string queryModel = null)
        {
            m_ollama = ollama;
            m_vectorDb = vectorDb;
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

                    if (m_vectorDb != null)
                    {
                        m_vectorDb.Dispose();
                    }
                }

                m_disposed = true;
            }
        }

        public async Task BeginLoadDocumentAsync(string documentId)
        {
            await m_vectorDb.EnsureCollectionAsync(m_collectionName, GetVectorSize());
            await m_vectorDb.BeginLoadDocumentAsync(m_collectionName, documentId);
        }

        private int GetVectorSize()
        {
            int vectorSize;
            switch (m_embeddingModel)
            {
                case "all-minilm":
                {
                    vectorSize = 384;
                    break;
                }
                case "embeddinggemma":
                {
                    vectorSize = 256;
                    break;
                }
                case "nomic-embed-text":
                {
                    vectorSize = 768;
                    break;
                }

                default:
                case "mxbai-embed-large":
                case "bge-m3":
                case "bge-large":
                case "snowflake-arctic-embed":
                case "snowflake-arctic-embed2":
                case "qwen3-embedding":
                {
                    vectorSize = 1024;
                    break;
                }
            }

            return vectorSize;
        }

        public async Task EndLoadDocumentAsync(string documentId)
        {
            await m_vectorDb.EndLoadDocumentAsync(m_collectionName, documentId);
        }

        public async Task IndexDocumentAsync(string documentId, string chunkId, string content)
        {
            try
            {
                await EmbedAndStoreAsync(documentId, chunkId, content);
            }
            catch (EmbeddingServerException ex) when (ex.StatusCode == 500)
            {
                // Split the chunk and retry
                const string headerTerminator = "---\n";
                var terminatorIndex = content.IndexOf(headerTerminator);

                if (terminatorIndex < 0)
                {
                    throw new InvalidOperationException($"Chunk {documentId}:{chunkId} failed with HTTP 500 and cannot be split: no header terminator found.");
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

                if (Guid.TryParse(chunkId, out Guid guid))
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
                    await EmbedAndStoreAsync(documentId, chunkId, firstContent);
                    await EmbedAndStoreAsync(documentId, secondId, secondContent);
                    Console.WriteLine($"WARNING: Chunk {chunkId} was split into two chunks ({chunkId} and {secondId}).");
                }
                catch (EmbeddingServerException)
                {
                    throw new InvalidOperationException($"Chunk {chunkId} failed embedding after split. Original error: {ex.Message}");
                }
            }
        }

        private async Task EmbedAndStoreAsync(string documentId, string chunkId, string content)
        {
            var vector = await m_ollama.EmbedAsync(content, m_embeddingModel);
            await m_vectorDb.UpsertAsync(m_collectionName, documentId, chunkId, vector, content);
        }

        public async Task<string> AskAsync(string question)
        {
            // Embed query
            var embedding = await m_ollama.EmbedAsync(question, m_embeddingModel);

            // Search vector DB
            var docs = await m_vectorDb.SearchAsync(m_collectionName, embedding, topK: 5);

            var context = string.Join("\n\n", docs);
            var prompt = $"Use the following context to answer the question:\n\n{context}\n\nQuestion: {question}";

            return await m_ollama.GenerateAsync(prompt, m_queryModel);
        }
    }
}
