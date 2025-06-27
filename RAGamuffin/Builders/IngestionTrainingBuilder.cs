using Microsoft.SemanticKernel.Connectors.SqliteVec;
using RAGamuffin.Abstractions;
using RAGamuffin.Core;
using RAGamuffin.Enums;
using RAGamuffin.Ingestion;
using RAGamuffin.VectorStores;

namespace RAGamuffin.Builders;
public class IngestionTrainingBuilder
{
    private IIngestionEngine _ingestionEngine;
    private IEmbedder _embedder;
    private IIngestionOptions _ingestionOptions;
    private IVectorStore _vectorStore;
    private string[] _trainingFiles = [];
    private EmbeddingProviders _embeddingProvider;
    private bool _dropDatabaseAndRetrainOnLoad = false;

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

            _ => throw new ArgumentException(
               $"Unsupported database model: {vectorDatabaseModel.GetType().Name}",
               nameof(vectorDatabaseModel))
        };

        return this;
    }

    public IngestionTrainingBuilder WithStrategy(IngestionStrategy strategy)
    {
        _ingestionOptions = strategy switch
        {
            IngestionStrategy.HybridParagraphWithThreshold => new PdfHybridParagraphIngestionOptions(),
            _ => throw new NotSupportedException($"Ingestion strategy {strategy} is not supported.")
        };

        return this;
    }

    public IngestionTrainingBuilder DropDatabaseAndRetrain(bool dropDatabaseAndRetrainOnLoad = false)
    {
        _dropDatabaseAndRetrainOnLoad = dropDatabaseAndRetrainOnLoad;

        return this;
    }

    public IngestionTrainingBuilder WithIngestionOptions(IIngestionOptions options)
    {
        _ingestionOptions = options ?? throw new ArgumentNullException(nameof(options), "Ingestion options cannot be null.");
        return this;
    }

    public IngestionTrainingBuilder WithTrainingFiles(string[] trainingFiles)
    {
        _trainingFiles = trainingFiles ?? throw new ArgumentNullException(nameof(trainingFiles), "Training files cannot be null.");
        return this;
    }

    public IIngestionEngine Build()
    {
        if (_embedder == null)
        {
            throw new InvalidOperationException("Embedding model must be set before building the ingestion engine.");
        }

        if (_vectorStore == null)
        {
            throw new InvalidOperationException("Vector database must be set before building the ingestion engine.");
        }

        if (_ingestionOptions == null)
        {
            throw new InvalidOperationException("Ingestion options must be set before building the ingestion engine.");
        }

        _ingestionEngine = new PdfIngestionEngine(_embedder, _vectorStore, _ingestionOptions, _trainingFiles, _dropDatabaseAndRetrainOnLoad);

        return _ingestionEngine;
    }
}

/*

var engine = new IngestionBuilder(EmbeddingProviders.Onnx)
                .WithEmbeddingModel(new OnnxEmbeddingModel("path/to/model.onnx", "path/to/tokenizer.json))
                .WithVectorDatabase(new SqliteDatabaseModel("path/to/database", "database_name.db", "collection_name")
                .WithStrategy(IngestionStrategy.Chunking)
                .WithDropDatabaseAndRetrain(true)
                .WithTrainingFiles(new string[] { "path/to/file/file1.txt", "path/to/file/file2.txt" })
                .WithIngestionOptions(new PdfHybridParagraphIngestionOptions
                {
                    MinSize = 0,
                    MaxSize = 1200,
                    Overlap = 500,
                    UseMetadata = true
                })
                .Build();

*/