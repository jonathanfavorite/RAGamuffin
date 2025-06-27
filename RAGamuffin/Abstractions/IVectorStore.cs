namespace RAGamuffin.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IVectorStore : IDisposable
{
    Task UpsertAsync(string id, float[] vector, IDictionary<string, object> metaData);
    Task<IEnumerable<(string Key, float Score, IDictionary<string, object> MetaData)>> SearchAsync(float[] query, int topK);
}
