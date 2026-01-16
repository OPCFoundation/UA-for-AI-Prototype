
using System.Net.Http.Json;
using System.Text.Json;

namespace Opc.Ua.RagUtility
{
    public class OllamaEmbeddingResponse
    {
        public float[] Embedding { get; set; }
    }

    public class OllamaClient : IDisposable
    {
        private readonly HttpClient m_http;
        private bool m_disposed;

        public OllamaClient(Uri baseUrl, TimeSpan timeout)
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

        public async Task<float[]> EmbedAsync(string text, string model = "mxbai-embed-large")
        {
            var response = await m_http.PostAsJsonAsync("/api/embeddings", new
            {
                model,
                prompt = text
            });

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
            return result.Embedding;
        }

        public async Task<string> GenerateAsync(string prompt, string model = "gpt-oss:120b-cloud")
        {
            var system =
                @"You are an assistant that answers ONLY using the provided context.
                If the answer is not fully contained in the context, say:
                'I don't know based on the provided data.'
                Do NOT use outside knowledge.
                Do NOT guess.
                Do NOT hallucinate.
                Do NOT use bolding in response.";

            var response = await m_http.PostAsJsonAsync("/api/generate", new
            {
                model = model,
                system,
                prompt,
                stream = false
            });

            var text = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("response").GetString();
        }

        public async Task<string> DescribeImageAsync(
            string document,
            string section, 
            string caption, 
            string image, 
            string model = "llava")
        {
            string prompt = $"Describe this figure from the document {document}. " +
                            $"The figure is in section {section}. " +
                            $"The figure caption is {caption}. " +
                            $"OPC and OPC UA is a standard for industrial interoperability. Do NOT expand the acryonym." +
                            $"Explain what charts, data, or objects are visible.";

            var response = await m_http.PostAsJsonAsync("/api/generate", new
            {
                model,
                prompt,
                stream = false,
                images = new[] { image }
            });

            var text = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("response").GetString();
        }
    }
}
