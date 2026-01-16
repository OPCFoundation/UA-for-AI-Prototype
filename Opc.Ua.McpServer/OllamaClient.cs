using System.Net.Http.Json;
using System.Text.Json;

namespace Opc.Ua.McpServer
{
    public class OllamaClient : IDisposable
    {
        private readonly HttpClient _http;
        private bool _disposed;

        public OllamaClient(string baseUrl, TimeSpan? timeout = null)
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = timeout ?? TimeSpan.FromSeconds(300)
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

        public async Task<float[]> EmbedAsync(string text, string model = "mxbai-embed-large")
        {
            var response = await _http.PostAsJsonAsync("/api/embeddings", new
            {
                model,
                prompt = text
            });

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
            return result?.Embedding ?? Array.Empty<float>();
        }

        public async Task<string> GenerateAsync(string prompt, string model, string systemPrompt = null)
        {
            var system = systemPrompt ??
                @"You are an assistant that answers ONLY using the provided context.
                If the answer is not fully contained in the context, say:
                'I don't know based on the provided data.'
                Do NOT use outside knowledge.
                Do NOT guess.
                Do NOT hallucinate.";

            var response = await _http.PostAsJsonAsync("/api/generate", new
            {
                model,
                system,
                prompt,
                stream = false
            });

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("response").GetString() ?? string.Empty;
        }
    }

    public class OllamaEmbeddingResponse
    {
        public float[] Embedding { get; set; }
    }
}
