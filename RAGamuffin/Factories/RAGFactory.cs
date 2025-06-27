using RAGamuffin.Abstractions;
using RAGamuffin.Common;
using RAGamuffin.Configuration;
using RAGamuffin.Embedding;
using RAGamuffin.Ingestion.Engines;
using RAGamuffin.Models;
using RAGamuffin.VectorStores.Providers;

namespace RAGamuffin.Factories;

/// <summary>
/// Factory for creating and configuring complete RAG systems
/// </summary>
public static class RAGFactory
{
    /// <summary>
    /// Creates a complete RAG system with all components configured
    /// </summary>
    /// <param name="configuration">Configuration for the RAG system</param>
    /// <returns>A fully configured RAG system</returns>
    public static SimpleRAGSystem CreateRAGSystem(RAGConfiguration configuration)
    {
        configuration.Validate();
        
        // Create embedder
        var embedder = CreateEmbedder(configuration);
        
        // Create vector store
        var vectorStore = CreateVectorStore(configuration);
        
        // Create ingestion engines
        var engines = CreateIngestionEngines();
        
        return new SimpleRAGSystem(embedder, vectorStore, engines, configuration);
    }
    
    /// <summary>
    /// Creates an embedder based on configuration
    /// </summary>
    public static IEmbedder CreateEmbedder(RAGConfiguration configuration)
    {
        return new OnnxEmbedder(configuration.EmbeddingModelPath, configuration.EmbeddingTokenizerPath);
    }
    
    /// <summary>
    /// Creates a vector store based on configuration
    /// </summary>
    public static IVectorStore CreateVectorStore(RAGConfiguration configuration)
    {
        if (configuration.RecreateDatabase)
        {
            DbHelper.DeleteSqliteDatabase(configuration.FullDatabasePath);
            DbHelper.CreateSqliteDatabase(configuration.FullDatabasePath);
        }
        
        return new SqliteVectorStoreProvider(configuration.FullDatabasePath, configuration.CollectionName);
    }
    
    /// <summary>
    /// Creates all available ingestion engines
    /// </summary>
    public static Dictionary<string, IIngestionEngine> CreateIngestionEngines()
    {
        return new Dictionary<string, IIngestionEngine>
        {
            ["pdf"] = new PdfIngestionEngine(),
            ["text"] = new TextIngestionEngine()
        };
    }
    
    /// <summary>
    /// Creates PDF ingestion options based on configuration
    /// </summary>
    public static PdfHybridParagraphIngestionOptions CreatePdfOptions(RAGConfiguration configuration)
    {
        return new PdfHybridParagraphIngestionOptions
        {
            MinSize = 0,
            MaxSize = configuration.ChunkSize,
            Overlap = configuration.ChunkOverlap,
            UseMetadata = configuration.UseMetadata
        };
    }
    
    /// <summary>
    /// Creates text ingestion options based on configuration
    /// </summary>
    public static TextHybridParagraphIngestionOptions CreateTextOptions(RAGConfiguration configuration)
    {
        return new TextHybridParagraphIngestionOptions
        {
            MinSize = 0,
            MaxSize = configuration.ChunkSize,
            Overlap = configuration.ChunkOverlap,
            UseMetadata = configuration.UseMetadata
        };
    }
}

/// <summary>
/// Simplified RAG system that abstracts away the complexity of the original example
/// </summary>
public class SimpleRAGSystem : IDisposable
{
    private readonly IEmbedder _embedder;
    private readonly IVectorStore _vectorStore;
    private readonly Dictionary<string, IIngestionEngine> _engines;
    private readonly RAGConfiguration _configuration;
    private bool _disposed = false;

    internal SimpleRAGSystem(
        IEmbedder embedder, 
        IVectorStore vectorStore, 
        Dictionary<string, IIngestionEngine> engines,
        RAGConfiguration configuration)
    {
        _embedder = embedder;
        _vectorStore = vectorStore;
        _engines = engines;
        _configuration = configuration;
    }

    /// <summary>
    /// Ingests documents from a directory, processing all supported file types
    /// </summary>
    /// <param name="directoryPath">Path to directory containing documents</param>
    /// <param name="searchPattern">File search pattern (default: all files)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of chunks ingested</returns>
    public async Task<int> IngestDirectoryAsync(
        string directoryPath, 
        string searchPattern = "*.*", 
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories);
        return await IngestFilesAsync(files, cancellationToken);
    }

    /// <summary>
    /// Ingests a single file
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of chunks ingested</returns>
    public async Task<int> IngestFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await IngestFilesAsync(new[] { filePath }, cancellationToken);
    }

    /// <summary>
    /// Ingests multiple files
    /// </summary>
    /// <param name="filePaths">Array of file paths</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of chunks ingested</returns>
    public async Task<int> IngestFilesAsync(string[] filePaths, CancellationToken cancellationToken = default)
    {
        // Check if we should skip ingestion because database already has data
        if (!_configuration.RecreateDatabase && await HasExistingDataAsync())
        {
            Console.WriteLine("📂 Database already contains data and RecreateDatabase=false");
            Console.WriteLine("   Skipping ingestion process. Set RecreateDatabase=true to re-ingest.");
            Console.WriteLine("   Or delete the database file manually to start fresh.");
            return 0;
        }

        var allIngestedItems = new List<IngestedItem>();
        
        var pdfOptions = RAGFactory.CreatePdfOptions(_configuration);
        var textOptions = RAGFactory.CreateTextOptions(_configuration);

        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Warning: File not found: {filePath}");
                continue;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            List<IngestedItem> fileItems;

            try
            {
                switch (extension)
                {
                    case ".pdf":
                        if (!_engines.ContainsKey("pdf"))
                            throw new NotSupportedException("PDF ingestion engine not available");
                        fileItems = await _engines["pdf"].IngestAsync(filePath, pdfOptions, cancellationToken);
                        break;
                    default:
                        if (!_engines.ContainsKey("text"))
                            throw new NotSupportedException("Text ingestion engine not available");
                        fileItems = await _engines["text"].IngestAsync(filePath, textOptions, cancellationToken);
                        break;
                }

                Console.WriteLine($"Processed: {Path.GetFileName(filePath)} -> {fileItems.Count} chunks");
                allIngestedItems.AddRange(fileItems);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {filePath}: {ex.Message}");
                // Continue with other files
            }
        }

        // Create embeddings and store vectors
        var storedCount = 0;
        foreach (var item in allIngestedItems)
        {
            try
            {
                item.Vectors = await _embedder.EmbedAsync(item.Text, cancellationToken);
                
                var metadata = new Dictionary<string, object>
                {
                    ["text"] = item.Text,
                    ["Length"] = item.Text.Length,
                    ["source"] = item.Source,
                    ["id"] = item.Id
                };

                await _vectorStore.UpsertAsync(item.Id, item.Vectors, metadata);
                storedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error storing vector for chunk {item.Id}: {ex.Message}");
                // Continue with other items
            }
        }

        Console.WriteLine($"Successfully ingested {storedCount} chunks from {filePaths.Length} files");
        return storedCount;
    }

    /// <summary>
    /// Performs semantic search
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="resultCount">Number of results to return (optional, uses configuration default)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results with scores and metadata</returns>
    public async Task<IEnumerable<(string Key, float Score, IDictionary<string, object> MetaData)>> SearchAsync(
        string query, 
        int? resultCount = null, 
        CancellationToken cancellationToken = default)
    {
        var count = resultCount ?? _configuration.DefaultSearchResultCount;
        var queryVector = await _embedder.EmbedAsync(query, cancellationToken);
        return await _vectorStore.SearchAsync(queryVector, count);
    }

    /// <summary>
    /// Gets the context text from search results for use with LLMs
    /// </summary>
    /// <param name="searchResults">Results from SearchAsync</param>
    /// <returns>Combined context text</returns>
    public static string ExtractContextFromResults(IEnumerable<(string Key, float Score, IDictionary<string, object> MetaData)> searchResults)
    {
        var contextTexts = searchResults
            .Where(r => r.MetaData?.ContainsKey("text") == true)
            .Select(r => r.MetaData["text"].ToString())
            .Where(text => !string.IsNullOrEmpty(text))
            .ToList();

        return string.Join("\n\n", contextTexts);
    }

    /// <summary>
    /// Gets the embedder instance (for advanced usage)
    /// </summary>
    public IEmbedder GetEmbedder() => _embedder;

    /// <summary>
    /// Gets the vector store instance (for advanced usage)
    /// </summary>
    public IVectorStore GetVectorStore() => _vectorStore;

    /// <summary>
    /// Gets the configuration (for advanced usage)
    /// </summary>
    public RAGConfiguration GetConfiguration() => _configuration;

    /// <summary>
    /// Checks if the database already contains data
    /// </summary>
    /// <returns>True if the database has existing vectors</returns>
    private async Task<bool> HasExistingDataAsync()
    {
        try
        {
            // Create a test vector with the correct dimension from the embedder
            var testVector = new float[_embedder.Dimension];
            
            // Try to perform a simple search to see if there's any data
            var results = await _vectorStore.SearchAsync(testVector, 1);
            var hasData = results.Any();
            
            if (hasData)
            {
                var count = results.Count();
                Console.WriteLine($"📊 Found existing data in database (at least {count} vectors)");
            }
            
            return hasData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"📂 Unable to check existing data: {ex.Message}");
            // If search fails, assume database is empty or doesn't exist
            return false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _vectorStore?.Dispose();
            _disposed = true;
        }
    }
} 