using Microsoft.SemanticKernel.Connectors.SqliteVec;
using RAGamuffin.Abstractions;
using RAGamuffin.Core;
using RAGamuffin.Enums;
using RAGamuffin.Factories;
using RAGamuffin.Ingestion;
using RAGamuffin.VectorStores;

namespace RAGamuffin.Builders;
public class IngestionTrainingBuilder
{
    private IEmbedder _embedder;
    private IVectorStore _vectorStore;
    private string[] _trainingFiles = [];
    private Dictionary<string, IIngestionOptions> _fileTypeOptions = new();
    private TrainingStrategy _trainingStrategy = TrainingStrategy.RetrainFromScratch;
    private bool _allowTextTraining = false;

    public IngestionTrainingBuilder WithEmbeddingModel(IEmbedder embedder)
    {
        _embedder = embedder;
        return this;
    }

    public IngestionTrainingBuilder WithVectorDatabase(IVectorDatabaseModel vectorDatabaseModel)
    {
        _vectorStore = vectorDatabaseModel switch
        {
            SqliteDatabaseModel sqliteDb => new SqliteVectorStoreProvider(sqliteDb.SqliteDbPath, sqliteDb.CollectionName),
            _ => throw new ArgumentException($"Unsupported database model: {vectorDatabaseModel.GetType().Name}", nameof(vectorDatabaseModel))
        };

        return this;
    }

    public IngestionTrainingBuilder WithTrainingStrategy(TrainingStrategy strategy)
    {
        _trainingStrategy = strategy;
        return this;
    }

    /// <summary>
    /// Sets the training files to be processed. 
    /// - Required for RetrainFromScratch strategy
    /// - Optional for incremental strategies (files can be provided when calling Train())
    /// - Optional for ProcessOnly strategy (only needed if you plan to call training methods)
    /// </summary>
    /// <param name="trainingFiles">Array of file paths to process</param>
    /// <returns>The builder instance for method chaining</returns>
    public IngestionTrainingBuilder WithTrainingFiles(string[] trainingFiles)
    {
        _trainingFiles = trainingFiles ?? throw new ArgumentNullException(nameof(trainingFiles), "Training files cannot be null.");
        return this;
    }

    public IngestionTrainingBuilder WithFileTypeOptions(string fileExtension, IIngestionOptions options)
    {
        _fileTypeOptions[fileExtension.ToLowerInvariant()] = options ?? throw new ArgumentNullException(nameof(options), "Ingestion options cannot be null.");
        return this;
    }

    public IngestionTrainingBuilder WithPdfOptions(IIngestionOptions options)
    {
        return WithFileTypeOptions(".pdf", options);
    }

    public IngestionTrainingBuilder WithTextOptions(IIngestionOptions options)
    {
        _fileTypeOptions["*"] = options;
        return this;
    }

    /// <summary>
    /// Creates a pipeline configured for state checking and document management operations.
    /// This is useful when you only need to query the vector store without training.
    /// </summary>
    /// <param name="embedder">The embedding model to use for search operations</param>
    /// <param name="vectorDatabaseModel">The vector database configuration</param>
    /// <returns>A builder configured for state management operations</returns>
    /// <remarks>
    /// This method creates a builder with TrainingStrategy.ProcessOnly, which means:
    /// - No training files are required
    /// - You can perform search operations
    /// - You can check document counts and IDs
    /// - You can delete individual documents
    /// - You cannot perform training operations
    /// </remarks>
    public static IngestionTrainingBuilder CreateForStateManagement(IEmbedder embedder, IVectorDatabaseModel vectorDatabaseModel)
    {
        return new IngestionTrainingBuilder()
            .WithEmbeddingModel(embedder)
            .WithVectorDatabase(vectorDatabaseModel)
            .WithTrainingStrategy(TrainingStrategy.ProcessOnly);
    }

    public RAGamuffinModel Build()
    {
        ValidateConfiguration();

        return new RAGamuffinModel(_embedder, _vectorStore, _trainingStrategy, _fileTypeOptions);
    }

    public async Task<List<IngestedItem>> Train(CancellationToken cancellationToken = default)
    {
        return await Train(_trainingFiles, cancellationToken);
    }

    public async Task<List<IngestedItem>> Train(string[] trainingFiles, CancellationToken cancellationToken = default)
    {
        var model = Build();
        
        // If no training files and using ProcessOnly, return empty list
        if ((trainingFiles == null || trainingFiles.Length == 0) && _trainingStrategy == TrainingStrategy.ProcessOnly)
        {
            return new List<IngestedItem>();
        }
        
        // For incremental strategies, validate that files are provided
        if ((trainingFiles == null || trainingFiles.Length == 0) && 
            (_trainingStrategy == TrainingStrategy.IncrementalAdd || _trainingStrategy == TrainingStrategy.IncrementalUpdate))
        {
            throw new InvalidOperationException("Training files must be provided when using incremental training strategies.");
        }
        
        return await model.Train(trainingFiles, cancellationToken);
    }

    /// <summary>
    /// Trains the model with text content directly (no files required)
    /// Perfect for real-time data ingestion scenarios
    /// </summary>
    /// <param name="textItems">Array of text items to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing the ingested items and the trained model</returns>
    public async Task<(List<IngestedItem> IngestedItems, RAGamuffinModel Model)> TrainWithText(TextItem[] textItems, CancellationToken cancellationToken = default)
    {
        _allowTextTraining = true;
        var model = Build();
        _allowTextTraining = false; // Reset for future calls
        
        // Validate that text items are provided
        if (textItems == null || textItems.Length == 0)
        {
            throw new InvalidOperationException("Text items must be provided for training.");
        }
        
        var ingestedItems = await model.TrainWithText(textItems, cancellationToken);
        return (ingestedItems, model);
    }

    public async Task<List<IngestedItem>> BuildAndIngestAsync(CancellationToken cancellationToken = default)
    {
        var model = Build();
        
        // If no training files and using ProcessOnly, return empty list
        if ((_trainingFiles == null || _trainingFiles.Length == 0) && _trainingStrategy == TrainingStrategy.ProcessOnly)
        {
            return new List<IngestedItem>();
        }
        
        return await model.IngestAndTrain(_trainingFiles, cancellationToken);
    }

    public async Task<List<IngestedItem>> BuildAsync(CancellationToken cancellationToken = default)
    {
        var model = Build();
        
        // If no training files and using ProcessOnly, return empty list
        if ((_trainingFiles == null || _trainingFiles.Length == 0) && _trainingStrategy == TrainingStrategy.ProcessOnly)
        {
            return new List<IngestedItem>();
        }
        
        return await model.Ingest(_trainingFiles, cancellationToken);
    }

    public IIngestionEngine BuildSingleEngine()
    {
        ValidateConfiguration();

        // If no training files and using ProcessOnly, throw appropriate exception
        if ((_trainingFiles == null || _trainingFiles.Length == 0) && _trainingStrategy == TrainingStrategy.ProcessOnly)
        {
            throw new InvalidOperationException("Cannot build single engine without training files.");
        }

        var factory = new IngestionEngineFactory();
        
        var groupedFiles = _trainingFiles.GroupBy(Path.GetExtension).ToDictionary(g => g.Key.ToLowerInvariant(), g => g.ToArray());
        
        if (groupedFiles.Count == 1)
        {
            return factory.CreateEngine(_trainingFiles);
        }
        
        throw new InvalidOperationException("Multiple file types detected. Use Train() for mixed file types.");
    }

    private void ValidateConfiguration()
    {
        if (_embedder == null)
        {
            throw new InvalidOperationException("Embedding model must be set before building.");
        }

        if (_vectorStore == null)
        {
            throw new InvalidOperationException("Vector database must be set before building.");
        }

        // Only require training files for file-based training, not for text-based
        if ((_trainingStrategy == TrainingStrategy.RetrainFromScratch) &&
            (_trainingFiles == null || _trainingFiles.Length == 0) &&
            !_allowTextTraining)
        {
            throw new InvalidOperationException("Training files must be set before building when using RetrainFromScratch strategy.");
        }
    }
}