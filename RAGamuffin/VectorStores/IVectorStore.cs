namespace RAGamuffin.VectorStores;
public interface IVectorStore
{
    Task UpsertAsync(string id, float[] vector, IDictionary<string, object> metaData);
    Task<IEnumerable<(string Key, float Score, IDictionary<string, object> MetaData)>> SearchAsync(float[] query, int topK);
}
