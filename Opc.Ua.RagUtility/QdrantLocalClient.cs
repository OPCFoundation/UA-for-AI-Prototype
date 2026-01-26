using System.Net.Http.Json;
using System.Text.Json;

namespace Opc.Ua.RagUtility
{
    public class QdrantLocalClient : IDisposable
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

        public async Task UpsertAsync(string collection, QdrantPoint point)
        {
            var body = new
            {
                points = new[]
                {
                    new
                    {
                        id = point?.Id.ToString() ?? "0",
                        vector = new Dictionary<string, float[]>
                        {
                            { "default", point.Vector }   // MUST use vector name
                        },
                        payload = point.Payload
                    }
                }
            };

            var response = await m_http.PutAsJsonAsync($"/collections/{collection}/points", body);
            var text = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            response = await m_http.GetAsync($"/collections/{collection}/points/{point?.Id}");
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
