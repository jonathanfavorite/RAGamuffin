using RAGamuffin.Abstractions;
using RAGamuffin.Enums;
using RAGamuffin.Factories;
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

    internal RAGamuffinModel(
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
} 