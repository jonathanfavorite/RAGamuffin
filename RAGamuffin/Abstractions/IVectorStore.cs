namespace RAGamuffin.Abstractions;
public interface IVectorStore
{
    Task UpsertAsync(string id, float[] vector, IDictionary<string, object> metaData);
    Task<IEnumerable<(string Key, float Score, IDictionary<string, object> MetaData)>> SearchAsync(float[] query, int topK);
    Task<IEnumerable<(string Key, float Score, IDictionary<string, object> MetaData)>> SearchAsync(string query, IEmbedder embedder, int topK, CancellationToken cancellationToken = default);
    Task DropCollectionAsync();
}
