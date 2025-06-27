using Microsoft.SemanticKernel.Connectors.SqliteVec;
using Microsoft.Extensions.VectorData;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Text.Json;

namespace RAGamuffin.VectorStores;
public class SqliteVectorStoreProvider : IVectorStore
{
    private readonly SqliteVectorStore _store;
    private readonly SqliteCollection<string, MicrosoftVectorRecord> _collection;

    public SqliteVectorStoreProvider(string sqliteDbPath, string collectionName)
    {
        // Build the ADO.NET connection string
        var connString = $"Data Source={sqliteDbPath}";

        // Pass (connectionString, collectionName) per API
        _collection = new SqliteCollection<string, MicrosoftVectorRecord>(
            connString,
            collectionName
        );

        // Ensure tables and vector virtual tables exist
        _collection.EnsureCollectionExistsAsync().GetAwaiter().GetResult();
    }

    public async Task UpsertAsync(
            string id,
            float[] vector,
            IDictionary<string, object> metaData)
    {
        var record = new MicrosoftVectorRecord
        {
            Id = id,
            Embedding = vector,
            MetaJson = metaData != null ? JsonSerializer.Serialize(metaData) : null
        };
        await _collection.UpsertAsync(record).ConfigureAwait(false);
    }

    public async Task<IEnumerable<(string Key, float Score, IDictionary<string, object>? MetaData)>>
    SearchAsync(float[] query, int topK)
    {
        // 1) Kick off the vector search
        var asyncResults = _collection.SearchAsync(query, topK);

        // 2) Prepare the list with matching tuple types
        var list = new List<(string Key, float Score, IDictionary<string, object>?)>();

        // 3) Iterate and cast
        await foreach (var r in asyncResults.ConfigureAwait(false))
        {
            IDictionary<string, object>? meta = null;
            if (!string.IsNullOrEmpty(r.Record.MetaJson))
            {
                try
                {
                    meta = JsonSerializer.Deserialize<Dictionary<string, object>>(r.Record.MetaJson!);
                }
                catch { /* ignore deserialization errors */ }
            }
            list.Add((
                Key: r.Record.Id,
                Score: r.Score.HasValue
                             ? (float)r.Score.Value
                             : 0f,
                MetaData: meta
            ));
        }

        return list;
    }
}
