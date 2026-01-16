using System.Text.Json.Serialization;

namespace Opc.Ua.RagUtility
{
    public class QdrantPoint
    {
        public object Id { get; set; }
        public float[] Vector { get; set; }
        public Dictionary<string, object> Payload { get; set; }
    }

    public class QdrantSearchResult
    {
        [JsonPropertyName("result")]
        public List<QdrantScoredPoint> Result { get; set; }
    }

    public class QdrantScoredPoint
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("score")]
        public float Score { get; set; }
        [JsonPropertyName("payload")]
        public Dictionary<string, object> Payload { get; set; }
    }
}