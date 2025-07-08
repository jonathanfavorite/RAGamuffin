using RAGamuffin.Abstractions;
using RAGamuffin.Enums;
using RAGamuffin.Factories;
using RAGamuffin.Helpers;
using RAGamuffin.Ingestion;

namespace RAGamuffin.Core;
public class RAGamuffinModel
{
    public IEmbedder Embedder { get; }
    public IVectorStore VectorStore { get; }
    public IIngestionEngineFactory EngineFactory { get; }
    public MultiFileIngestionManager IngestionManager { get; }
    public Dictionary<string, IIngestionOptions> FileTypeOptions { get; }
    public TrainingStrategy TrainingStrategy { get; }

    public RAGamuffinModel(
        IEmbedder embedder,
        IVectorStore vectorStore,
        TrainingStrategy trainingStrategy,
        Dictionary<string, IIngestionOptions> fileTypeOptions)
    {
        Embedder = embedder;
        VectorStore = vectorStore;
        TrainingStrategy = trainingStrategy;
        FileTypeOptions = fileTypeOptions;
        
        EngineFactory = new IngestionEngineFactory();
        IngestionManager = new MultiFileIngestionManager(EngineFactory, embedder, vectorStore, trainingStrategy);
        
        foreach (var option in fileTypeOptions)
        {
            IngestionManager.WithFileTypeOptions(option.Key, option.Value);
        }
    }

    public async Task<List<IngestedItem>> Train(string[] trainingFiles, CancellationToken cancellationToken = default)
    {
        switch (TrainingStrategy)
        {
            case TrainingStrategy.RetrainFromScratch:
                await VectorStore.DropCollectionAsync();
                break;
                
            case TrainingStrategy.IncrementalAdd:
            case TrainingStrategy.IncrementalUpdate:
                // Don't drop collection - we'll handle existing documents in the ingestion process
                break;
                
            case TrainingStrategy.ProcessOnly:
                // Don't perform any vector operations
                return await IngestionManager.IngestFilesAsync(trainingFiles, false, cancellationToken);
        }

        Console.WriteLine("Chunking data...");
        var ingestedItems = await IngestionManager.IngestFilesAsync(trainingFiles, true, cancellationToken);
        Console.WriteLine($"Successfully chunked {ingestedItems.Count} items");

        Console.WriteLine("Vectorizing & Saving Data...");
        var processedCount = 0;
        var skippedCount = 0;
        var updatedCount = 0;
        
        foreach (IngestedItem ingestedItem in ingestedItems)
        {
            bool shouldProcess = true;
            
            if (TrainingStrategy == TrainingStrategy.IncrementalAdd)
            {
                // Check if document already exists
                if (await VectorStore.DocumentExistsAsync(ingestedItem.Id))
                {
                    shouldProcess = false;
                    skippedCount++;
                }
            }
            else if (TrainingStrategy == TrainingStrategy.IncrementalUpdate)
            {
                // Check if document exists to track updates vs new additions
                if (await VectorStore.DocumentExistsAsync(ingestedItem.Id))
                {
                    updatedCount++;
                }
            }
            
            if (shouldProcess)
            {
                ingestedItem.Vectors = await Embedder.EmbedAsync(ingestedItem.Text, cancellationToken);
                await VectorStore.UpsertAsync(ingestedItem.Id, ingestedItem.Vectors, ingestedItem.Metadata);
                processedCount++;
            }
        }
        
        Console.WriteLine($"Vectorization complete!");
        Console.WriteLine($"Processed: {processedCount}, Skipped: {skippedCount}, Updated: {updatedCount}");

        return ingestedItems;
    }

    /// <summary>
    /// Trains the model with text content directly (no files required)
    /// Perfect for real-time data ingestion scenarios
    /// </summary>
    /// <param name="textItems">Array of text items to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of ingested items</returns>
    public async Task<List<IngestedItem>> TrainWithText(TextItem[] textItems, CancellationToken cancellationToken = default)
    {
        switch (TrainingStrategy)
        {
            case TrainingStrategy.RetrainFromScratch:
                await VectorStore.DropCollectionAsync();
                break;
                
            case TrainingStrategy.IncrementalAdd:
            case TrainingStrategy.IncrementalUpdate:
                // Don't drop collection - we'll handle existing documents in the ingestion process
                break;
                
            case TrainingStrategy.ProcessOnly:
                // Don't perform any vector operations
                return await ProcessTextItemsAsync(textItems, false, cancellationToken);
        }

        Console.WriteLine("Processing text data...");
        var ingestedItems = await ProcessTextItemsAsync(textItems, true, cancellationToken);
        Console.WriteLine($"Successfully processed {ingestedItems.Count} text items");

        Console.WriteLine("Vectorizing & Saving Data...");
        var processedCount = 0;
        var skippedCount = 0;
        var updatedCount = 0;
        var totalCount = ingestedItems.Count;
        var startTime = DateTime.Now;
        
        // Process items with progress tracking
        for (int i = 0; i < ingestedItems.Count; i++)
        {
            var ingestedItem = ingestedItems[i];
            bool shouldProcess = true;
            
            if (TrainingStrategy == TrainingStrategy.IncrementalAdd)
            {
                // Check if document already exists
                if (await VectorStore.DocumentExistsAsync(ingestedItem.Id))
                {
                    shouldProcess = false;
                    skippedCount++;
                }
            }
            else if (TrainingStrategy == TrainingStrategy.IncrementalUpdate)
            {
                // Check if document exists to track updates vs new additions
                if (await VectorStore.DocumentExistsAsync(ingestedItem.Id))
                {
                    updatedCount++;
                }
            }
            
            if (shouldProcess)
            {
                // Note: Vectors are already computed during batch embedding, so we don't need to embed again
                await VectorStore.UpsertAsync(ingestedItem.Id, ingestedItem.Vectors, ingestedItem.Metadata);
                processedCount++;
            }
            
            // Progress reporting every 10 items or at the end
            if ((i + 1) % 10 == 0 || i == ingestedItems.Count - 1)
            {
                var timeSinceStart = DateTime.Now - startTime;
                var progress = (double)(i + 1) / totalCount;
                var estimatedTotalTime = TimeSpan.FromSeconds(timeSinceStart.TotalSeconds / progress);
                var estimatedRemaining = estimatedTotalTime - timeSinceStart;
                
                Console.WriteLine($"Vectorizing progress: {i + 1}/{totalCount} ({progress:P1})... (Total: {timeSinceStart.TotalMinutes:F1}m, Est. remaining: {estimatedRemaining.TotalMinutes:F1}m)");
            }
        }
        
        var totalTime = DateTime.Now - startTime;
        Console.WriteLine($"Vectorization complete! Total time: {totalTime.TotalMinutes:F1} minutes");
        Console.WriteLine($"Processed: {processedCount}, Skipped: {skippedCount}, Updated: {updatedCount}");

        return ingestedItems;
    }

    /// <summary>
    /// Processes text items and optionally performs vector operations
    /// </summary>
    private async Task<List<IngestedItem>> ProcessTextItemsAsync(TextItem[] textItems, bool performVectorOperations, CancellationToken cancellationToken = default)
    {
        var allItems = new List<IngestedItem>();
        
        // Get text processing options
        var textOptions = FileTypeOptions.TryGetValue("*", out var options) 
            ? options as TextHybridParagraphIngestionOptions ?? new TextHybridParagraphIngestionOptions()
            : new TextHybridParagraphIngestionOptions();

        // Process items in batches for better performance
        const int batchSize = 32; // Process 32 items at a time
        var processedCount = 0;
        var totalCount = textItems.Length;
        
        Console.WriteLine($"Processing {totalCount} items in batches of {batchSize}...");

        // First pass: collect all texts that need embedding
        var textsToEmbed = new List<string>();
        var itemsToEmbed = new List<IngestedItem>();
        
        foreach (var textItem in textItems)
        {
            // Validate text item
            if (textItem == null)
            {
                Console.WriteLine("Warning: Skipping null TextItem");
                continue;
            }
            
            if (string.IsNullOrEmpty(textItem.Content))
            {
                Console.WriteLine($"Warning: Skipping TextItem with null or empty content. ID: {textItem.Id}");
                continue;
            }
            
            // Check if chunking should be skipped for this item
            bool shouldSkipChunking = textItem.SkipChunking || !textOptions.EnableChunking;
            
            if (shouldSkipChunking)
            {
                // Store as single item without chunking
                var metadata = new Dictionary<string, object>
                {
                    ["text"] = textItem.Content,
                    ["source"] = textItem.Id,
                    ["timestamp"] = textItem.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["chunked"] = false
                };

                // Merge with any custom metadata from the TextItem
                if (textItem.Metadata != null)
                {
                    foreach (var kvp in textItem.Metadata)
                    {
                        // Don't override system metadata keys
                        if (!metadata.ContainsKey(kvp.Key))
                        {
                            metadata[kvp.Key] = kvp.Value;
                        }
                    }
                }
                
                var ingestedItem = new IngestedItem
                {
                    Id = textItem.Id,
                    Text = textItem.Content,
                    Source = textItem.Id,
                    Vectors = null, // Will be set after batch embedding
                    Metadata = metadata
                };
                
                textsToEmbed.Add(textItem.Content);
                itemsToEmbed.Add(ingestedItem);
                allItems.Add(ingestedItem);
            }
            else
            {
                // Apply normal chunking logic
                var chunks = ChunkingHelper.ChunkTextFixedSize(
                    textItem.Content, 
                    textOptions.MaxSize, 
                    textOptions.Overlap
                );

                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    var chunkId = $"{textItem.Id}_chunk_{i}";
                    
                    // Create base metadata
                    var metadata = new Dictionary<string, object>
                    {
                        ["text"] = chunk,
                        ["source"] = textItem.Id,
                        ["chunk_index"] = i,
                        ["total_chunks"] = chunks.Count,
                        ["timestamp"] = textItem.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        ["chunked"] = true
                    };

                    // Merge with any custom metadata from the TextItem
                    if (textItem.Metadata != null)
                    {
                        foreach (var kvp in textItem.Metadata)
                        {
                            // Don't override system metadata keys
                            if (!metadata.ContainsKey(kvp.Key))
                            {
                                metadata[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                    
                    var ingestedItem = new IngestedItem
                    {
                        Id = chunkId,
                        Text = chunk,
                        Source = textItem.Id,
                        Vectors = null, // Will be set after batch embedding
                        Metadata = metadata
                    };

                    textsToEmbed.Add(chunk);
                    itemsToEmbed.Add(ingestedItem);
                    allItems.Add(ingestedItem);
                }
            }
        }

        // Now perform batch embedding
        if (performVectorOperations && textsToEmbed.Count > 0)
        {
            Console.WriteLine($"Performing batch embedding for {textsToEmbed.Count} items...");
            
            const int embeddingBatchSize = 100; // Process 100 items at a time
            var totalBatches = (textsToEmbed.Count + embeddingBatchSize - 1) / embeddingBatchSize;
            var currentBatch = 0;
            var startTime = DateTime.Now;
            var lastBatchTime = startTime;
            
            for (int i = 0; i < textsToEmbed.Count; i += embeddingBatchSize)
            {
                currentBatch++;
                var currentBatchSize = Math.Min(embeddingBatchSize, textsToEmbed.Count - i);
                var batchTexts = textsToEmbed.Skip(i).Take(currentBatchSize).ToArray();
                
                var timeSinceStart = DateTime.Now - startTime;
                var estimatedTotalTime = TimeSpan.FromSeconds((timeSinceStart.TotalSeconds / currentBatch) * totalBatches);
                var estimatedRemaining = estimatedTotalTime - timeSinceStart;
                Console.WriteLine($"Processing batch {currentBatch}/{totalBatches} ({i + currentBatchSize}/{textsToEmbed.Count} items)... (Total: {timeSinceStart.TotalMinutes:F1}m, Est. remaining: {estimatedRemaining.TotalMinutes:F1}m)");
                
                var batchStartTime = DateTime.Now;
                var batchEmbeddings = await Embedder.EmbedBatchAsync(batchTexts, cancellationToken);
                var batchTime = DateTime.Now - batchStartTime;
                
                // Assign embeddings back to items
                for (int j = 0; j < currentBatchSize && i + j < itemsToEmbed.Count; j++)
                {
                    if (j < batchEmbeddings.Length)
                    {
                        itemsToEmbed[i + j].Vectors = batchEmbeddings[j];
                    }
                }
                
                lastBatchTime = DateTime.Now;
                Console.WriteLine($"Completed batch {currentBatch}/{totalBatches} in {batchTime.TotalSeconds:F1}s");
            }
            
            var totalTime = DateTime.Now - startTime;
            Console.WriteLine($"Batch embedding complete! Total time: {totalTime.TotalMinutes:F1} minutes");
        }

        return allItems;
    }

    public async Task<List<IngestedItem>> Ingest(string[] trainingFiles, CancellationToken cancellationToken = default)
    {
        return await IngestionManager.IngestFilesAsync(trainingFiles, false, cancellationToken);
    }

    public async Task<List<IngestedItem>> IngestAndTrain(string[] trainingFiles, CancellationToken cancellationToken = default)
    {
        return await IngestionManager.IngestFilesAsync(trainingFiles, true, cancellationToken);
    }

    public async Task<IEnumerable<(string Key, float Score, IDictionary<string, object> MetaData)>> Search(string query, int topK, CancellationToken cancellationToken = default)
    {
        return await VectorStore.SearchAsync(query, Embedder, topK, cancellationToken);
    }

    public async Task<IEnumerable<(string Key, float Score, IDictionary<string, object> MetaData)>> Search(float[] queryVector, int topK, CancellationToken cancellationToken = default)
    {
        return await VectorStore.SearchAsync(queryVector, topK);
    }

    public async Task<string[]> SearchAndReturnTexts(string query, int topK, CancellationToken cancellationToken = default)
    {
        return await VectorStore.SearchAndReturnTexts(query, Embedder, topK, cancellationToken);
    }
    
    // New methods for incremental training management
    public async Task<int> GetDocumentCount()
    {
        return await VectorStore.GetDocumentCountAsync();
    }
    
    public async Task<IEnumerable<string>> GetDocumentIds()
    {
        return await VectorStore.GetDocumentIdsAsync();
    }
    
    public async Task DeleteDocument(string documentId)
    {
        await VectorStore.DeleteDocumentAsync(documentId);
    }
    
    public async Task DeleteDocuments(IEnumerable<string> documentIds)
    {
        await VectorStore.DeleteDocumentsAsync(documentIds);
    }

    // New metadata retrieval methods
    public async Task<IDictionary<string, object>?> GetDocumentMetadata(string documentId)
    {
        return await VectorStore.GetDocumentMetadataAsync(documentId);
    }

    public async Task<IEnumerable<(string DocumentId, IDictionary<string, object>? Metadata)>> GetAllDocumentsMetadata()
    {
        return await VectorStore.GetAllDocumentsMetadataAsync();
    }

    public async Task<IEnumerable<(string DocumentId, IDictionary<string, object>? Metadata)>> GetDocumentsByMetadataFilter(
        string metadataKey, 
        object metadataValue, 
        CancellationToken cancellationToken = default)
    {
        return await VectorStore.GetDocumentsByMetadataFilterAsync(metadataKey, metadataValue, cancellationToken);
    }

    public async Task<IEnumerable<(string DocumentId, IDictionary<string, object>? Metadata)>> GetDocumentsByMetadataRange(
        string metadataKey, 
        object minValue, 
        object maxValue, 
        CancellationToken cancellationToken = default)
    {
        return await VectorStore.GetDocumentsByMetadataRangeAsync(metadataKey, minValue, maxValue, cancellationToken);
    }

    public async Task<IEnumerable<string>> GetDocumentIdsByMetadataFilter(
        string metadataKey, 
        object metadataValue, 
        CancellationToken cancellationToken = default)
    {
        return await VectorStore.GetDocumentIdsByMetadataFilterAsync(metadataKey, metadataValue, cancellationToken);
    }
} 