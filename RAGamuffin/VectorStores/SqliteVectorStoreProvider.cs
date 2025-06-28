using Microsoft.SemanticKernel.Connectors.SqliteVec;
using Microsoft.Extensions.VectorData;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Text.Json;
using RAGamuffin.Abstractions;
using RAGamuffin.Helpers;
using RAGamuffin.VectorStores.Models;
using System.Threading;

namespace RAGamuffin.VectorStores;
public class SqliteVectorStoreProvider : IVectorStore
{
    private readonly SqliteVectorStore _store;
    private readonly SqliteCollection<string, MicrosoftVectorRecord> _collection;

    public SqliteVectorStoreProvider(string sqliteDbPath, string collectionName, bool retrainData = false)
    {

        if(retrainData)
        {
            DbHelper.DeleteSqliteDatabase(sqliteDbPath);
            DbHelper.CreateSqliteDatabase(sqliteDbPath);
        }

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
        return await SearchAsync(query, topK, CancellationToken.None);
    }

    public async Task<IEnumerable<(string Key, float Score, IDictionary<string, object>? MetaData)>>
    SearchAsync(string query, IEmbedder embedder, int topK, CancellationToken cancellationToken = default)
    {
        var queryVector = await embedder.EmbedAsync(query, cancellationToken);
        return await SearchAsync(queryVector, topK, cancellationToken);
    }

    public async Task<string[]> SearchAndReturnTexts(string query, IEmbedder embedder, int topK, CancellationToken cancellationToken = default)
    {
        List<string> texts = new();

        var results = await SearchAsync(query, embedder, topK, cancellationToken);
        foreach (var result in results)
        {
            // Extract the original text from metadata for LLM context
            if (result.MetaData != null && result.MetaData.ContainsKey("text"))
            {
                string text = result.MetaData["text"].ToString() ?? "";
                texts.Add(text);
            }
        }

        return texts.ToArray();
    }

    private async Task<IEnumerable<(string Key, float Score, IDictionary<string, object>? MetaData)>>
    SearchAsync(float[] query, int topK, CancellationToken cancellationToken)
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

    public async Task DropCollectionAsync()
    {
        // Get all keys from the collection using a dummy search with a very large topK
        var allKeys = new List<string>();
        
        // Get the vector dimension from the MicrosoftVectorRecord attribute
        var vectorDimension = 768; // Default dimension from [VectorStoreVector(768)]
        
        try
        {
            // Use a dummy vector (all zeros) to search for all records
            var dummyVector = new float[vectorDimension];
            
            // Search with a large topK to get all records (10000 should be sufficient for most use cases)
            var searchResults = _collection.SearchAsync(dummyVector, 10000);
            
            await foreach (var result in searchResults.ConfigureAwait(false))
            {
                allKeys.Add(result.Record.Id);
            }
            
            // If we found any keys, delete them all at once
            if (allKeys.Count > 0)
            {
                await _collection.DeleteAsync(allKeys).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {

        }
    }
}
