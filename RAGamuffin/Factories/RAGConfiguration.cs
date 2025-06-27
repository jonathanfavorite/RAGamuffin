namespace RAGamuffin.Factories;

/// <summary>
/// Configuration class for RAG system setup with sensible defaults
/// </summary>
public class RAGConfiguration
{
    // ============================================================================
    // EMBEDDING CONFIGURATION
    // ============================================================================
    
    /// <summary>
    /// Path to the ONNX embedding model file
    /// </summary>
    public string EmbeddingModelPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Path to the embedding tokenizer file
    /// </summary>
    public string EmbeddingTokenizerPath { get; set; } = string.Empty;

    // ============================================================================
    // DATABASE CONFIGURATION
    // ============================================================================
    
    /// <summary>
    /// Directory for storing the vector database
    /// </summary>
    public string DatabaseDirectory { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the SQLite database file
    /// </summary>
    public string DatabaseName { get; set; } = "vectors.db";
    
    /// <summary>
    /// Name of the vector collection in the database
    /// </summary>
    public string CollectionName { get; set; } = "documents";
    
    /// <summary>
    /// Whether to recreate the database (delete existing and create fresh)
    /// </summary>
    public bool RecreateDatabase { get; set; } = false;

    // ============================================================================
    // INGESTION CONFIGURATION
    // ============================================================================
    
    /// <summary>
    /// Chunk size for document processing (in characters)
    /// </summary>
    public int ChunkSize { get; set; } = 1200;
    
    /// <summary>
    /// Overlap between chunks (in characters)
    /// </summary>
    public int ChunkOverlap { get; set; } = 500;
    
    /// <summary>
    /// Whether to include metadata in ingested items
    /// </summary>
    public bool UseMetadata { get; set; } = true;

    // ============================================================================
    // SEARCH CONFIGURATION
    // ============================================================================
    
    /// <summary>
    /// Default number of search results to retrieve
    /// </summary>
    public int DefaultSearchResultCount { get; set; } = 5;

    // ============================================================================
    // HELPER PROPERTIES
    // ============================================================================
    
    /// <summary>
    /// Gets the full path to the database file
    /// </summary>
    public string FullDatabasePath => Path.Combine(DatabaseDirectory, DatabaseName);

    // ============================================================================
    // VALIDATION
    // ============================================================================
    
    /// <summary>
    /// Validates that all required configuration is provided
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EmbeddingModelPath))
            throw new InvalidOperationException("EmbeddingModelPath is required");
            
        if (string.IsNullOrWhiteSpace(EmbeddingTokenizerPath))
            throw new InvalidOperationException("EmbeddingTokenizerPath is required");
            
        if (string.IsNullOrWhiteSpace(DatabaseDirectory))
            throw new InvalidOperationException("DatabaseDirectory is required");
            
        if (!File.Exists(EmbeddingModelPath))
            throw new FileNotFoundException($"Embedding model file not found: {EmbeddingModelPath}");
            
        if (!File.Exists(EmbeddingTokenizerPath))
            throw new FileNotFoundException($"Embedding tokenizer file not found: {EmbeddingTokenizerPath}");
            
        if (!Directory.Exists(DatabaseDirectory))
        {
            Directory.CreateDirectory(DatabaseDirectory);
        }
        
        if (ChunkSize <= 0)
            throw new ArgumentException("ChunkSize must be greater than 0");
            
        if (ChunkOverlap < 0)
            throw new ArgumentException("ChunkOverlap cannot be negative");
            
        if (ChunkOverlap >= ChunkSize)
            throw new ArgumentException("ChunkOverlap must be less than ChunkSize");
    }
} 