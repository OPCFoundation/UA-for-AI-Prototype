using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Opc.Ua.McpServer
{
    public class QdrantClient : IDisposable
    {
        private readonly HttpClient _http;
        private bool _disposed;

        public QdrantClient(string baseUrl, TimeSpan? timeout = null)
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = timeout ?? TimeSpan.FromSeconds(60)
            };
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _http?.Dispose();
                }
                _disposed = true;
            }
        }

        public async Task<List<QdrantSearchHit>> SearchAsync(
            string collection,
            float[] vector,
            int topK = 5,
            Dictionary<string, object> filter = null)
        {
            var body = new Dictionary<string, object>
            {
                ["vector"] = new { name = "default", vector },
                ["limit"] = topK,
                ["with_payload"] = true
            };

            if (filter != null)
            {
                body["filter"] = filter;
            }

            var response = await _http.PostAsJsonAsync($"/collections/{collection}/points/search", body);
            response.EnsureSuccessStatusCode();

            var text = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<QdrantSearchResult>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.Result ?? new List<QdrantSearchHit>();
        }

        public async Task<List<string>> SearchContentAsync(
            string collection,
            float[] vector,
            int topK = 5)
        {
            var hits = await SearchAsync(collection, vector, topK);
            return hits
                .Where(h => h.Payload != null && h.Payload.ContainsKey("content"))
                .Select(h => h.Payload["content"]?.ToString() ?? string.Empty)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }
    }

    public class QdrantSearchResult
    {
        [JsonPropertyName("result")]
        public List<QdrantSearchHit> Result { get; set; }
    }

    public class QdrantSearchHit
    {
        [JsonPropertyName("id")]
        public object Id { get; set; }

        [JsonPropertyName("score")]
        public float Score { get; set; }

        [JsonPropertyName("payload")]
        public Dictionary<string, object> Payload { get; set; }
    }
}
