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
    private EmbeddingProviders _embeddingProvider;
    private bool _dropDatabaseAndRetrainOnLoad = false;
    private Dictionary<string, IIngestionOptions> _fileTypeOptions = new();

    public IngestionTrainingBuilder(EmbeddingProviders embeddingProvider)
    {
        _embeddingProvider = embeddingProvider;
    }

    public IngestionTrainingBuilder WithEmbeddingModel(IEmbedder embedder)
    {
        _embedder = embedder;
        return this;
    }

    public IngestionTrainingBuilder WithVectorDatabase(IVectorDatabaseModel vectorDatabaseModel)
    {
        _vectorStore = vectorDatabaseModel switch
        {
            SqliteDatabaseModel sqliteDb => new SqliteVectorStoreProvider(sqliteDb.SqliteDbPath, sqliteDb.CollectionName, sqliteDb.RetrainData),
            _ => throw new ArgumentException($"Unsupported database model: {vectorDatabaseModel.GetType().Name}", nameof(vectorDatabaseModel))
        };

        return this;
    }

    public IngestionTrainingBuilder DropDatabaseAndRetrain(bool dropDatabaseAndRetrainOnLoad = false)
    {
        _dropDatabaseAndRetrainOnLoad = dropDatabaseAndRetrainOnLoad;
        return this;
    }

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

    public async Task<List<IngestedItem>> BuildAndIngestAsync(CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        var factory = new IngestionEngineFactory();
        var manager = new MultiFileIngestionManager(factory, _embedder, _vectorStore, _dropDatabaseAndRetrainOnLoad);

        foreach (var option in _fileTypeOptions)
        {
            manager.WithFileTypeOptions(option.Key, option.Value);
        }

        return await manager.IngestFilesAsync(_trainingFiles, cancellationToken);
    }

    public IIngestionEngine BuildSingleEngine()
    {
        ValidateConfiguration();

        var factory = new IngestionEngineFactory();
        
        var groupedFiles = _trainingFiles.GroupBy(Path.GetExtension).ToDictionary(g => g.Key.ToLowerInvariant(), g => g.ToArray());
        
        if (groupedFiles.Count == 1)
        {
            return factory.CreateEngine(_trainingFiles);
        }
        
        throw new InvalidOperationException("Multiple file types detected. Use BuildAndIngestAsync() for mixed file types.");
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

        if (_trainingFiles == null || _trainingFiles.Length == 0)
        {
            throw new InvalidOperationException("Training files must be set before building.");
        }
    }
}

/*

// Single file type example
var items = await new IngestionTrainingBuilder(EmbeddingProviders.Onnx)
    .WithEmbeddingModel(new OnnxEmbeddingModel("path/to/model.onnx", "path/to/tokenizer.json"))
    .WithVectorDatabase(new SqliteDatabaseModel("path/to/database", "database_name.db", "collection_name"))
    .WithPdfOptions(new PdfHybridParagraphIngestionOptions
    {
        MinSize = 0,
        MaxSize = 1200,
        Overlap = 500,
        UseMetadata = true
    })
    .WithTrainingFiles(new string[] { "path/to/file1.pdf", "path/to/file2.pdf" })
    .DropDatabaseAndRetrain(true)
    .BuildAndIngestAsync();

// Multiple file types example (PDF + any other file type)
var items = await new IngestionTrainingBuilder(EmbeddingProviders.Onnx)
    .WithEmbeddingModel(new OnnxEmbeddingModel("path/to/model.onnx", "path/to/tokenizer.json"))
    .WithVectorDatabase(new SqliteDatabaseModel("path/to/database", "database_name.db", "collection_name"))
    .WithPdfOptions(new PdfHybridParagraphIngestionOptions
    {
        MinSize = 0,
        MaxSize = 1200,
        Overlap = 500,
        UseMetadata = true
    })
    .WithTextOptions(new TextHybridParagraphIngestionOptions
    {
        MinSize = 500,
        MaxSize = 1000,
        Overlap = 200,
        UseMetadata = true
    })
    .WithTrainingFiles(new string[] { 
        "path/to/file1.pdf", 
        "path/to/file2.txt", 
        "path/to/file3.md",
        "path/to/file4.html"
    })
    .DropDatabaseAndRetrain(true)
    .BuildAndIngestAsync();

*/