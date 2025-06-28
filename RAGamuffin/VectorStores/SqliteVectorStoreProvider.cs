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
using System.IO;

namespace RAGamuffin.VectorStores;
public class SqliteVectorStoreProvider : IVectorStore
{
    private readonly SqliteVectorStore _store;
    private readonly SqliteCollection<string, MicrosoftVectorRecord> _collection;
    private readonly string _databasePath;

    public SqliteVectorStoreProvider(string sqliteDbPath, string collectionName)
    {
        _databasePath = sqliteDbPath;
        
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

        return list.OrderByDescending(x => x.Score);
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
            // If search fails, the collection might already be empty or there might be an issue
            // We'll just continue - the collection will be effectively empty
        }
    }

    // New methods for incremental training
    public async Task<bool> DocumentExistsAsync(string documentId)
    {
        try
        {
            // Try to get the document by ID
            var record = await _collection.GetAsync(documentId);
            return record != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> GetDocumentCountAsync()
    {
        var count = 0;
        
        Console.WriteLine($"DEBUG: Counting documents using direct collection access");
        
        try
        {
            // Use a simple approach: try to get a small sample and count them
            // This is more reliable than dummy vector search
            var searchResults = _collection.SearchAsync(new float[768], 1000);
            await foreach (var result in searchResults.ConfigureAwait(false))
            {
                count++;
                if (count <= 3) // Only log first few for debugging
                {
                    Console.WriteLine($"DEBUG: Counted document {result.Record.Id}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Exception during count: {ex.Message}");
            Console.WriteLine($"DEBUG: Exception type: {ex.GetType().Name}");
            
            // Try alternative approach: use GetAllDocumentsMetadataAsync
            try
            {
                var allDocs = await GetAllDocumentsMetadataAsync();
                count = allDocs.Count();
                Console.WriteLine($"DEBUG: Counted {count} documents using metadata approach");
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"DEBUG: Alternative count also failed: {ex2.Message}");
                return 0;
            }
        }
        
        Console.WriteLine($"DEBUG: Final document count: {count}");
        return count;
    }

    public async Task<IEnumerable<string>> GetDocumentIdsAsync()
    {
        var documentIds = new List<string>();
        var vectorDimension = 768;
        var dummyVector = new float[vectorDimension];
        
        try
        {
            var searchResults = _collection.SearchAsync(dummyVector, int.MaxValue);
            await foreach (var result in searchResults.ConfigureAwait(false))
            {
                documentIds.Add(result.Record.Id);
            }
        }
        catch (Exception ex)
        {
            // Return empty list if search fails
        }
        
        return documentIds;
    }

    public async Task DeleteDocumentAsync(string documentId)
    {
        await _collection.DeleteAsync(documentId).ConfigureAwait(false);
    }

    public async Task DeleteDocumentsAsync(IEnumerable<string> documentIds)
    {
        await _collection.DeleteAsync(documentIds.ToList()).ConfigureAwait(false);
    }

    // New metadata retrieval methods
    public async Task<IDictionary<string, object>?> GetDocumentMetadataAsync(string documentId)
    {
        try
        {
            var record = await _collection.GetAsync(documentId);
            if (record?.MetaJson != null)
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(record.MetaJson);
            }
        }
        catch { /* ignore errors */ }
        return null;
    }

    public async Task<IEnumerable<(string DocumentId, IDictionary<string, object>? Metadata)>> GetAllDocumentsMetadataAsync()
    {
        var results = new List<(string DocumentId, IDictionary<string, object>? Metadata)>();
        var vectorDimension = 768;
        var dummyVector = new float[vectorDimension];
        
        try
        {
            var searchResults = _collection.SearchAsync(dummyVector, int.MaxValue);
            await foreach (var result in searchResults.ConfigureAwait(false))
            {
                IDictionary<string, object>? metadata = null;
                if (!string.IsNullOrEmpty(result.Record.MetaJson))
                {
                    try
                    {
                        metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(result.Record.MetaJson);
                    }
                    catch { /* ignore deserialization errors */ }
                }
                results.Add((result.Record.Id, metadata));
            }
        }
        catch (Exception ex)
        {
            // Return empty list if search fails
        }
        
        return results;
    }

    public async Task<IEnumerable<(string DocumentId, IDictionary<string, object>? Metadata)>> GetDocumentsByMetadataFilterAsync(
        string metadataKey, 
        object metadataValue, 
        CancellationToken cancellationToken = default)
    {
        var allDocuments = await GetAllDocumentsMetadataAsync();
        var filteredResults = new List<(string DocumentId, IDictionary<string, object>? Metadata)>();
        
        foreach (var doc in allDocuments)
        {
            if (doc.Metadata?.TryGetValue(metadataKey, out var value) == true)
            {
                if (value?.Equals(metadataValue) == true)
                {
                    filteredResults.Add(doc);
                }
            }
        }
        
        return filteredResults;
    }

    public async Task<IEnumerable<(string DocumentId, IDictionary<string, object>? Metadata)>> GetDocumentsByMetadataRangeAsync(
        string metadataKey, 
        object minValue, 
        object maxValue, 
        CancellationToken cancellationToken = default)
    {
        var allDocuments = await GetAllDocumentsMetadataAsync();
        var filteredResults = new List<(string DocumentId, IDictionary<string, object>? Metadata)>();
        
        foreach (var doc in allDocuments)
        {
            if (doc.Metadata?.TryGetValue(metadataKey, out var value) == true)
            {
                if (IsInRange(value, minValue, maxValue))
                {
                    filteredResults.Add(doc);
                }
            }
        }
        
        return filteredResults;
    }

    public async Task<IEnumerable<string>> GetDocumentIdsByMetadataFilterAsync(
        string metadataKey, 
        object metadataValue, 
        CancellationToken cancellationToken = default)
    {
        var filteredDocs = await GetDocumentsByMetadataFilterAsync(metadataKey, metadataValue, cancellationToken);
        return filteredDocs.Select(doc => doc.DocumentId);
    }

    private bool IsInRange(object value, object minValue, object maxValue)
    {
        try
        {
            // Handle numeric comparisons
            if (value is IComparable comparable && minValue is IComparable minComparable && maxValue is IComparable maxComparable)
            {
                return comparable.CompareTo(minValue) >= 0 && comparable.CompareTo(maxValue) <= 0;
            }
            
            // Handle string comparisons
            if (value is string strValue && minValue is string minStr && maxValue is string maxStr)
            {
                return string.Compare(strValue, minStr, StringComparison.OrdinalIgnoreCase) >= 0 && 
                       string.Compare(strValue, maxStr, StringComparison.OrdinalIgnoreCase) <= 0;
            }
            
            // Handle DateTime comparisons
            if (value is DateTime dateValue && minValue is DateTime minDate && maxValue is DateTime maxDate)
            {
                return dateValue >= minDate && dateValue <= maxDate;
            }
        }
        catch { /* ignore comparison errors */ }
        
        return false;
    }
}
