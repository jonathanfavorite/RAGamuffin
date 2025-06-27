using Microsoft.SemanticKernel.Connectors.SqliteVec;
using System.Text.Json;
using RAGamuffin.Abstractions;
using RAGamuffin.Models;

namespace RAGamuffin.VectorStores.Providers;
public class SqliteVectorStoreProvider : IVectorStore, IDisposable
{
    private readonly SqliteVectorStore _store;
    private readonly SqliteCollection<string, VectorRecord> _collection;

    public SqliteVectorStoreProvider(string sqliteDbPath, string collectionName)
    {
        // Build the ADO.NET connection string
        var connString = $"Data Source={sqliteDbPath}";

        // Pass (connectionString, collectionName) per API
        _collection = new SqliteCollection<string, VectorRecord>(
            connString,
            collectionName
        );

        // Ensure tables and vector virtual tables exist
        _collection.EnsureCollectionExistsAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _collection?.Dispose();
    }

    public async Task UpsertAsync(
            string id,
            float[] vector,
            IDictionary<string, object> metaData)
    {
        var record = new VectorRecord
        {
            Id = id,
            Embedding = vector,
            MetaJson = metaData != null ? JsonSerializer.Serialize(metaData) : null
        };
        
        // Debug: Show what we're storing
        if (metaData?.ContainsKey("text") == true)
        {
            var text = metaData["text"].ToString();
            Console.WriteLine($"  [STORE] Storing text length: {text?.Length}, preview: {(text?.Length > 50 ? text[..50] + "..." : text)}");
        }
        
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
                    
                    // Debug: Show what we're retrieving
                    if (meta?.ContainsKey("text") == true)
                    {
                        var text = meta["text"].ToString();
                        Console.WriteLine($"  [RETRIEVE] Retrieved text length: {text?.Length}, preview: {(text?.Length > 50 ? text[..50] + "..." : text)}");
                    }
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
