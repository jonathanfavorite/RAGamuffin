using RAGamuffin.Abstractions;
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
    public bool DropDatabaseAndRetrainOnLoad { get; }

    internal RAGamuffinModel(
        IEmbedder embedder,
        IVectorStore vectorStore,
        bool dropDatabaseAndRetrainOnLoad,
        Dictionary<string, IIngestionOptions> fileTypeOptions)
    {
        Embedder = embedder;
        VectorStore = vectorStore;
        DropDatabaseAndRetrainOnLoad = dropDatabaseAndRetrainOnLoad;
        FileTypeOptions = fileTypeOptions;
        
        EngineFactory = new IngestionEngineFactory();
        IngestionManager = new MultiFileIngestionManager(EngineFactory, embedder, vectorStore, dropDatabaseAndRetrainOnLoad);
        
        foreach (var option in fileTypeOptions)
        {
            IngestionManager.WithFileTypeOptions(option.Key, option.Value);
        }
    }

    public async Task<List<IngestedItem>> Train(string[] trainingFiles, CancellationToken cancellationToken = default)
    {
        if (DropDatabaseAndRetrainOnLoad)
        {
            await VectorStore.DropCollectionAsync();
        }

        Console.WriteLine("Chunking data...");
        var ingestedItems = await IngestionManager.IngestFilesAsync(trainingFiles, false, cancellationToken);
        Console.WriteLine($"Successfully chunked {ingestedItems.Count} items");

        Console.WriteLine("Vectorizing & Saving Data...");
        foreach (IngestedItem ingestedItem in ingestedItems)
        {
            ingestedItem.Vectors = await Embedder.EmbedAsync(ingestedItem.Text, cancellationToken);
            await VectorStore.UpsertAsync(ingestedItem.Id, ingestedItem.Vectors, ingestedItem.Metadata);
        }
        Console.WriteLine("Vectorization complete!");

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
} 