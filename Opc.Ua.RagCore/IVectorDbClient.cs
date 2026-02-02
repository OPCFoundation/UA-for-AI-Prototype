namespace Opc.Ua.RagCore
{
    public interface IVectorDbClient : IDisposable
    {
        Task DeleteCollectionAsync(string collectionName);
        Task EnsureCollectionAsync(string collectionName, int vectorSize);
        Task BeginLoadDocumentAsync(string collectionName, string documentId);
        Task EndLoadDocumentAsync(string collectionName, string documentId);
        Task UpsertAsync(string collection, string documentId, string chunkId, float[] vector, string content);
        Task<List<string>> SearchAsync(string collection, float[] vector, int topK = 5);
    }
}
