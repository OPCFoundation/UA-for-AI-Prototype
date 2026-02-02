using System.Net.Http.Json;
using System.Text.Json;

namespace Opc.Ua.RagCore
{
    public class QdrantLocalClient : IVectorDbClient
    {
        private readonly HttpClient m_http;
        private bool m_disposed;

        public QdrantLocalClient(Uri baseUrl, TimeSpan timeout)
        {
            m_http = new HttpClient();
            m_http.BaseAddress = baseUrl;
            m_http.Timeout = timeout;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                if (disposing)
                {
                    if (m_http != null)
                    {
                        m_http.Dispose();
                    }
                }

                m_disposed = true;
            }
        }

        public async Task DeleteCollectionAsync(string collectionName)
        {
            var response = await m_http.DeleteAsync($"/collections/{collectionName}");
            var text = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();
        }

        // Ensure the collection exists
        public async Task EnsureCollectionAsync(string name, int vectorSize)
        {
            var body = new
            {
                vectors = new Dictionary<string, object>
                {
                    ["default"] = new
                    {
                        size = vectorSize,
                        distance = "Cosine"
                    }
                }
            };

            var response = await m_http.PutAsJsonAsync($"/collections/{name}", body);
            var text = await response.Content.ReadAsStringAsync();
            //response.EnsureSuccessStatusCode();
        }

        public async Task BeginLoadDocumentAsync(string collectionName, string documentId)
        {
        }

        public async Task EndLoadDocumentAsync(string collectionName, string documentId)
        {
        }

        public async Task UpsertAsync(string collection, string documentId, string chunkId, float[] vector, string content)
        {
            var body = new
            {
                points = new[]
                {
                    new
                    {
                        id = chunkId ?? "0",
                        vector = new Dictionary<string, float[]>
                        {
                            { "default", vector }   // MUST use vector name
                        },
                        payload = new Dictionary<string, object> { { "content", content } }
                    }
                }
            };

            var response = await m_http.PutAsJsonAsync($"/collections/{collection}/points", body);
            var text = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            response = await m_http.GetAsync($"/collections/{collection}/points/{chunkId}");
            text = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<string>> SearchAsync(string collection, float[] vector, int topK = 5)
        {
            var body = new
            {
                vector = new
                {
                    name = "default",
                    vector,
                },
                limit = topK,
                with_payload = true
            };

            var response = await m_http.PostAsJsonAsync($"/collections/{collection}/points/search", body);
            var text = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            var json = JsonSerializer.Deserialize<QdrantSearchResult>(text, new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            });

            return json.Result
                .Select(r => r.Payload["content"].ToString())
                .ToList();
        }
    }
}
