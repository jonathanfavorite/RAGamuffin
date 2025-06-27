namespace RAGamuffin.Factories;

/// <summary>
/// Builder pattern for creating RAGConfiguration with fluent API
/// </summary>
public class RAGConfigurationBuilder
{
    private readonly RAGConfiguration _configuration = new();

    /// <summary>
    /// Sets the embedding model and tokenizer paths
    /// </summary>
    /// <param name="modelPath">Path to the ONNX model file</param>
    /// <param name="tokenizerPath">Path to the tokenizer file</param>
    /// <returns>The builder instance</returns>
    public RAGConfigurationBuilder WithEmbedding(string modelPath, string tokenizerPath)
    {
        _configuration.EmbeddingModelPath = modelPath;
        _configuration.EmbeddingTokenizerPath = tokenizerPath;
        return this;
    }

    /// <summary>
    /// Sets the database configuration
    /// </summary>
    /// <param name="directory">Database directory</param>
    /// <param name="databaseName">Database file name (optional)</param>
    /// <param name="collectionName">Collection name (optional)</param>
    /// <returns>The builder instance</returns>
    public RAGConfigurationBuilder WithDatabase(
        string directory, 
        string databaseName = "vectors.db", 
        string collectionName = "documents")
    {
        _configuration.DatabaseDirectory = directory;
        _configuration.DatabaseName = databaseName;
        _configuration.CollectionName = collectionName;
        return this;
    }

    /// <summary>
    /// Configures whether to recreate the database
    /// </summary>
    /// <param name="recreate">True to delete and recreate the database</param>
    /// <returns>The builder instance</returns>
    public RAGConfigurationBuilder WithDatabaseRecreation(bool recreate = true)
    {
        _configuration.RecreateDatabase = recreate;
        return this;
    }

    /// <summary>
    /// Sets the chunking configuration
    /// </summary>
    /// <param name="chunkSize">Size of each chunk in characters</param>
    /// <param name="overlap">Overlap between chunks in characters</param>
    /// <returns>The builder instance</returns>
    public RAGConfigurationBuilder WithChunking(int chunkSize = 1200, int overlap = 500)
    {
        _configuration.ChunkSize = chunkSize;
        _configuration.ChunkOverlap = overlap;
        return this;
    }

    /// <summary>
    /// Sets whether to use metadata in ingestion
    /// </summary>
    /// <param name="useMetadata">True to include metadata</param>
    /// <returns>The builder instance</returns>
    public RAGConfigurationBuilder WithMetadata(bool useMetadata = true)
    {
        _configuration.UseMetadata = useMetadata;
        return this;
    }

    /// <summary>
    /// Sets the default number of search results
    /// </summary>
    /// <param name="count">Default number of search results</param>
    /// <returns>The builder instance</returns>
    public RAGConfigurationBuilder WithDefaultSearchResults(int count = 5)
    {
        _configuration.DefaultSearchResultCount = count;
        return this;
    }

    /// <summary>
    /// Sets up common defaults for typical RAG scenarios
    /// </summary>
    /// <param name="baseDirectory">Base directory for all RAG files</param>
    /// <returns>The builder instance</returns>
    public RAGConfigurationBuilder WithCommonDefaults(string baseDirectory)
    {
        return WithEmbedding(
                Path.Combine(baseDirectory, "model.onnx"),
                Path.Combine(baseDirectory, "tokenizer.json"))
            .WithDatabase(baseDirectory)
            .WithChunking()
            .WithMetadata()
            .WithDefaultSearchResults();
    }

    /// <summary>
    /// Sets up configuration for development/testing scenarios
    /// </summary>
    /// <param name="baseDirectory">Base directory for all RAG files</param>
    /// <returns>The builder instance</returns>
    public RAGConfigurationBuilder WithDevelopmentDefaults(string baseDirectory)
    {
        return WithCommonDefaults(baseDirectory)
            .WithDatabaseRecreation(true)  // Always recreate in dev
            .WithChunking(800, 200)        // Smaller chunks for testing
            .WithDefaultSearchResults(3);  // Fewer results for testing
    }

    /// <summary>
    /// Sets up configuration for production scenarios
    /// </summary>
    /// <param name="baseDirectory">Base directory for all RAG files</param>
    /// <returns>The builder instance</returns>
    public RAGConfigurationBuilder WithProductionDefaults(string baseDirectory)
    {
        return WithCommonDefaults(baseDirectory)
            .WithDatabaseRecreation(false) // Don't recreate in production
            .WithChunking(1200, 500)       // Larger chunks for better context
            .WithDefaultSearchResults(5);  // More results for better quality
    }

    /// <summary>
    /// Builds the final configuration
    /// </summary>
    /// <returns>The configured RAGConfiguration instance</returns>
    public RAGConfiguration Build()
    {
        return _configuration;
    }

    /// <summary>
    /// Builds the configuration and creates a RAG system in one step
    /// </summary>
    /// <returns>A fully configured SimpleRAGSystem</returns>
    public SimpleRAGSystem BuildSystem()
    {
        var config = Build();
        return RAGFactory.CreateRAGSystem(config);
    }
}

/// <summary>
/// Static factory methods for creating builders with common patterns
/// </summary>
public static class RAGBuilder
{
    /// <summary>
    /// Creates a new configuration builder
    /// </summary>
    /// <returns>A new RAGConfigurationBuilder instance</returns>
    public static RAGConfigurationBuilder Create()
    {
        return new RAGConfigurationBuilder();
    }

    /// <summary>
    /// Creates a builder with common defaults
    /// </summary>
    /// <param name="baseDirectory">Base directory containing model, tokenizer, and database</param>
    /// <returns>A preconfigured RAGConfigurationBuilder</returns>
    public static RAGConfigurationBuilder CreateWithDefaults(string baseDirectory)
    {
        return new RAGConfigurationBuilder().WithCommonDefaults(baseDirectory);
    }

    /// <summary>
    /// Creates a builder configured for development
    /// </summary>
    /// <param name="baseDirectory">Base directory containing model, tokenizer, and database</param>
    /// <returns>A development-configured RAGConfigurationBuilder</returns>
    public static RAGConfigurationBuilder CreateForDevelopment(string baseDirectory)
    {
        return new RAGConfigurationBuilder().WithDevelopmentDefaults(baseDirectory);
    }

    /// <summary>
    /// Creates a builder configured for production
    /// </summary>
    /// <param name="baseDirectory">Base directory containing model, tokenizer, and database</param>
    /// <returns>A production-configured RAGConfigurationBuilder</returns>
    public static RAGConfigurationBuilder CreateForProduction(string baseDirectory)
    {
        return new RAGConfigurationBuilder().WithProductionDefaults(baseDirectory);
    }

    /// <summary>
    /// Creates a RAG system with minimal configuration (one-liner!)
    /// </summary>
    /// <param name="baseDirectory">Base directory containing model, tokenizer, and database</param>
    /// <param name="forDevelopment">True for development defaults, false for production</param>
    /// <returns>A fully configured SimpleRAGSystem</returns>
    public static SimpleRAGSystem QuickStart(string baseDirectory, bool forDevelopment = true)
    {
        var builder = forDevelopment 
            ? CreateForDevelopment(baseDirectory)
            : CreateForProduction(baseDirectory);
            
        return builder.BuildSystem();
    }
} 