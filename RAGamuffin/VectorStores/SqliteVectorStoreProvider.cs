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
using Microsoft.Data.Sqlite;

namespace RAGamuffin.VectorStores;
public class SqliteVectorStoreProvider : IVectorStore
{
    private readonly SqliteVectorStore _store;
    private readonly SqliteCollection<string, MicrosoftVectorRecord> _collection;
    private readonly string _databasePath;
    private readonly int _vectorDimension;
    private readonly string _collectionName;

    public SqliteVectorStoreProvider(string sqliteDbPath, string collectionName, int vectorDimension = 768)
    {
        _databasePath = sqliteDbPath;
        _vectorDimension = vectorDimension;
        _collectionName = collectionName;
        
        // NOTE: The vector dimension is set at database creation time. If you change the embedding dimension,
        // you must manually delete/recreate the database and collection. No dynamic schema checks are performed.

        var connString = $"Data Source={sqliteDbPath}";
        _collection = new SqliteCollection<string, MicrosoftVectorRecord>(
            connString,
            collectionName
        );
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
        Console.WriteLine($"DEBUG: Dropping collection using improved approach");
        
        try
        {
            // Try direct SQL approach first
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();
            
            try
            {
                // Delete all records from the main table
                var deleteCommand = connection.CreateCommand();
                deleteCommand.CommandText = $"DELETE FROM {_collectionName}";
                var deletedCount = await deleteCommand.ExecuteNonQueryAsync();
                
                Console.WriteLine($"DEBUG: Deleted {deletedCount} records using direct SQL");
                
                // Also try to clear the vector table if it exists
                try
                {
                    var deleteVectorCommand = connection.CreateCommand();
                    deleteVectorCommand.CommandText = $"DELETE FROM vec_{_collectionName}";
                    await deleteVectorCommand.ExecuteNonQueryAsync();
                    Console.WriteLine($"DEBUG: Cleared vector table vec_{_collectionName}");
                }
                catch (Exception vecEx)
                {
                    Console.WriteLine($"DEBUG: Vector table cleanup failed (may not exist): {vecEx.Message}");
                }
                
                return;
            }
            catch (Exception sqlEx)
            {
                Console.WriteLine($"DEBUG: Direct SQL deletion failed: {sqlEx.Message}");
                
                // Fallback to batched deletion approach
                await DropCollectionUsingBatchedDeletion();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Drop collection failed: {ex.Message}");
            
            // Final fallback: try batched deletion
            try
            {
                await DropCollectionUsingBatchedDeletion();
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"DEBUG: Fallback deletion also failed: {fallbackEx.Message}");
                // If everything fails, the collection will be effectively empty after this
            }
        }
    }
    
    private async Task DropCollectionUsingBatchedDeletion()
    {
        Console.WriteLine($"DEBUG: Using batched deletion approach");
        
        // Get all keys using our improved method
        var allKeys = (await GetDocumentIdsAsync()).ToList();
        
        if (allKeys.Count > 0)
        {
            Console.WriteLine($"DEBUG: Deleting {allKeys.Count} records in batches");
            
            // Delete in batches to avoid overwhelming the system
            var batchSize = 100;
            for (int i = 0; i < allKeys.Count; i += batchSize)
            {
                var batch = allKeys.Skip(i).Take(batchSize).ToList();
                try
                {
                    await _collection.DeleteAsync(batch).ConfigureAwait(false);
                    Console.WriteLine($"DEBUG: Deleted batch {i / batchSize + 1}, {batch.Count} records");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DEBUG: Failed to delete batch: {ex.Message}");
                }
            }
        }
        else
        {
            Console.WriteLine($"DEBUG: No records found to delete");
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
        Console.WriteLine($"DEBUG: Counting documents using direct SQL approach");
        
        try
        {
            // Use direct SQL to count records - more reliable than dummy vector search
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();
            
            // Try to count from the main collection table
            var countCommand = connection.CreateCommand();
            countCommand.CommandText = $"SELECT COUNT(*) FROM {_collectionName}";
            
            var count = 0;
            try
            {
                var result = await countCommand.ExecuteScalarAsync();
                count = Convert.ToInt32(result);
                Console.WriteLine($"DEBUG: Direct SQL count: {count}");
            }
            catch (Exception sqlEx)
            {
                Console.WriteLine($"DEBUG: Direct SQL count failed: {sqlEx.Message}");
                
                // Fallback: Try with smaller batches using search
                count = await GetDocumentCountUsingBatchedSearch();
            }
            
            Console.WriteLine($"DEBUG: Final document count: {count}");
            return count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Exception during count: {ex.Message}");
            Console.WriteLine($"DEBUG: Exception type: {ex.GetType().Name}");
            
            // Final fallback: try the metadata approach
            try
            {
                var allDocs = await GetAllDocumentsMetadataAsync();
                var count = allDocs.Count();
                Console.WriteLine($"DEBUG: Counted {count} documents using metadata approach");
                return count;
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"DEBUG: Alternative count also failed: {ex2.Message}");
                return 0;
            }
        }
    }

    private async Task<int> GetDocumentCountUsingBatchedSearch()
    {
        Console.WriteLine($"DEBUG: Using batched search approach for counting");
        
        var totalCount = 0;
        var batchSize = 100; // Much smaller batch size
        var hasMoreResults = true;
        var maxBatches = 100; // Safety limit
        var currentBatch = 0;
        
        // Create a simple query vector (not all zeros)
        var queryVector = new float[_vectorDimension];
        for (int i = 0; i < Math.Min(10, _vectorDimension); i++)
        {
            queryVector[i] = 0.1f; // Small non-zero values
        }
        
        while (hasMoreResults && currentBatch < maxBatches)
        {
            try
            {
                var skip = currentBatch * batchSize;
                Console.WriteLine($"DEBUG: Counting batch {currentBatch + 1}, skip={skip}");
                
                // Search with smaller batch size
                var searchResults = _collection.SearchAsync(queryVector, batchSize);
                var batchCount = 0;
                
                await foreach (var result in searchResults.ConfigureAwait(false))
                {
                    batchCount++;
                }
                
                totalCount += batchCount;
                hasMoreResults = batchCount == batchSize; // If we got full batch, there might be more
                currentBatch++;
                
                Console.WriteLine($"DEBUG: Batch {currentBatch} found {batchCount} documents");
                
                if (batchCount == 0)
                {
                    hasMoreResults = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Batch search failed: {ex.Message}");
                hasMoreResults = false;
            }
        }
        
        Console.WriteLine($"DEBUG: Batched search total count: {totalCount}");
        return totalCount;
    }

    public async Task<IEnumerable<string>> GetDocumentIdsAsync()
    {
        Console.WriteLine($"DEBUG: Getting document IDs using direct SQL approach");
        
        try
        {
            // Use direct SQL to get IDs - more reliable than dummy vector search
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();
            
            var idsCommand = connection.CreateCommand();
            idsCommand.CommandText = $"SELECT Id FROM {_collectionName}";
            
            var documentIds = new List<string>();
            
            try
            {
                using var reader = await idsCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = reader.GetString(0);
                    documentIds.Add(id);
                }
                
                Console.WriteLine($"DEBUG: Direct SQL found {documentIds.Count} document IDs");
                return documentIds;
            }
            catch (Exception sqlEx)
            {
                Console.WriteLine($"DEBUG: Direct SQL ID retrieval failed: {sqlEx.Message}");
                
                // Fallback: Try with batched search
                return await GetDocumentIdsUsingBatchedSearch();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Error in GetDocumentIdsAsync: {ex.Message}");
            Console.WriteLine($"DEBUG: Exception type: {ex.GetType().Name}");
            return new List<string>();
        }
    }

    private async Task<IEnumerable<string>> GetDocumentIdsUsingBatchedSearch()
    {
        Console.WriteLine($"DEBUG: Using batched search approach for getting IDs");
        
        var allDocumentIds = new List<string>();
        var batchSize = 100; // Much smaller batch size
        var hasMoreResults = true;
        var maxBatches = 100; // Safety limit
        var currentBatch = 0;
        
        // Create a simple query vector (not all zeros)
        var queryVector = new float[_vectorDimension];
        for (int i = 0; i < Math.Min(10, _vectorDimension); i++)
        {
            queryVector[i] = 0.1f; // Small non-zero values
        }
        
        while (hasMoreResults && currentBatch < maxBatches)
        {
            try
            {
                Console.WriteLine($"DEBUG: Getting IDs batch {currentBatch + 1}");
                
                var searchResults = _collection.SearchAsync(queryVector, batchSize);
                var batchCount = 0;
                var seenIds = new HashSet<string>();
                
                await foreach (var result in searchResults.ConfigureAwait(false))
                {
                    if (!seenIds.Contains(result.Record.Id))
                    {
                        allDocumentIds.Add(result.Record.Id);
                        seenIds.Add(result.Record.Id);
                        batchCount++;
                    }
                }
                
                hasMoreResults = batchCount == batchSize;
                currentBatch++;
                
                Console.WriteLine($"DEBUG: Batch {currentBatch} found {batchCount} unique document IDs");
                
                if (batchCount == 0)
                {
                    hasMoreResults = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Batch ID search failed: {ex.Message}");
                hasMoreResults = false;
            }
        }
        
        // Remove duplicates and return
        var uniqueIds = allDocumentIds.Distinct().ToList();
        Console.WriteLine($"DEBUG: Batched search found {uniqueIds.Count} unique document IDs");
        return uniqueIds;
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
        
        try
        {
            // Get document IDs first using a smaller batch approach
            var documentIds = await GetDocumentIdsAsync();
            Console.WriteLine($"DEBUG: Retrieved {documentIds.Count()} document IDs");
            
            // Then retrieve metadata for each document individually
            var processedCount = 0;
            foreach (var documentId in documentIds)
            {
                try
                {
                    var metadata = await GetDocumentMetadataAsync(documentId);
                    results.Add((documentId, metadata));
                    processedCount++;
                    
                    if (processedCount % 100 == 0)
                    {
                        Console.WriteLine($"DEBUG: Processed {processedCount} documents");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DEBUG: Error getting metadata for {documentId}: {ex.Message}");
                    results.Add((documentId, null));
                }
            }
            
            Console.WriteLine($"DEBUG: Successfully processed {processedCount} documents");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Error in GetAllDocumentsMetadataAsync: {ex.Message}");
            Console.WriteLine($"DEBUG: Exception type: {ex.GetType().Name}");
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
