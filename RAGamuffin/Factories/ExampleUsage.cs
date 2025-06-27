using RAGamuffin.Factories;

namespace RAGamuffin.Examples;

/// <summary>
/// Example of how to use the RAGFactory for simplified RAG system setup
/// This replaces the complex manual setup from the original example
/// </summary>
public static class SimpleRAGExample
{
    /// <summary>
    /// Simple example showing how to use the RAG factory
    /// This replaces ~300 lines of complex setup code with ~20 lines
    /// </summary>
    public static async Task<string> RunSimpleExample()
    {
        // 1. Configure the RAG system (much simpler than the original!)
        var config = new RAGConfiguration
        {
            // Required paths
            EmbeddingModelPath = @"C:\RAGamuffin\model.onnx",
            EmbeddingTokenizerPath = @"C:\RAGamuffin\tokenizer.json",
            DatabaseDirectory = @"C:\RAGamuffin\",
            
            // Optional settings (these have sensible defaults)
            DatabaseName = "my_documents.db",
            CollectionName = "documents",
            RecreateDatabase = true,  // Start fresh
            ChunkSize = 1200,
            ChunkOverlap = 500,
            DefaultSearchResultCount = 5
        };

        // 2. Create the RAG system (factory handles all the complex setup!)
        using var ragSystem = RAGFactory.CreateRAGSystem(config);

        // 3. Ingest documents (much simpler than the original!)
        var documentsPath = @"C:\RAGamuffin\training-files\";
        var chunksIngested = await ragSystem.IngestDirectoryAsync(documentsPath, "*.pdf");
        Console.WriteLine($"Ingested {chunksIngested} chunks");

        // 4. Search (exactly the same as before, but simpler setup)
        var query = "What are the paid holidays at this company?";
        var results = await ragSystem.SearchAsync(query);

        // 5. Extract context for LLM (utility method provided)
        var context = SimpleRAGSystem.ExtractContextFromResults(results);

        // 6. Use with your favorite LLM library (InstructSharp, Semantic Kernel, etc.)
        // var response = await YourLLMService.GenerateResponse(query, context);

        return context; // For this example, just return the context
    }

    /// <summary>
    /// Example showing custom configuration and advanced usage
    /// </summary>
    public static async Task<string> RunAdvancedExample()
    {
        var config = new RAGConfiguration
        {
            EmbeddingModelPath = @"C:\RAGamuffin\model.onnx",
            EmbeddingTokenizerPath = @"C:\RAGamuffin\tokenizer.json",
            DatabaseDirectory = @"C:\RAGamuffin\",
            DatabaseName = "advanced_rag.db",
            CollectionName = "my_collection",
            RecreateDatabase = false, // Keep existing data
            ChunkSize = 800,          // Smaller chunks
            ChunkOverlap = 200,       // Less overlap
            DefaultSearchResultCount = 10
        };

        using var ragSystem = RAGFactory.CreateRAGSystem(config);

        // Ingest specific files instead of entire directory
        var filesToIngest = new[]
        {
            @"C:\docs\manual.pdf",
            @"C:\docs\policies.txt",
            @"C:\docs\handbook.pdf"
        };

        var chunksIngested = await ragSystem.IngestFilesAsync(filesToIngest);
        Console.WriteLine($"Ingested {chunksIngested} chunks from {filesToIngest.Length} files");

        // Perform multiple searches
        var queries = new[]
        {
            "What is the vacation policy?",
            "How do I submit expenses?",
            "What are the work from home rules?"
        };

        var allResults = new List<string>();
        foreach (var query in queries)
        {
            var results = await ragSystem.SearchAsync(query, resultCount: 3);
            var context = SimpleRAGSystem.ExtractContextFromResults(results);
            allResults.Add($"Query: {query}\nContext: {context}\n---");
        }

        return string.Join("\n", allResults);
    }

    /// <summary>
    /// Example showing how to access underlying components for advanced scenarios
    /// </summary>
    public static async Task RunAdvancedComponentAccess()
    {
        var config = new RAGConfiguration
        {
            EmbeddingModelPath = @"C:\RAGamuffin\model.onnx",
            EmbeddingTokenizerPath = @"C:\RAGamuffin\tokenizer.json",
            DatabaseDirectory = @"C:\RAGamuffin\"
        };

        using var ragSystem = RAGFactory.CreateRAGSystem(config);

        // Access underlying components for advanced usage
        var embedder = ragSystem.GetEmbedder();
        var vectorStore = ragSystem.GetVectorStore();
        var configuration = ragSystem.GetConfiguration();

        // Use embedder directly
        var customEmbedding = await embedder.EmbedAsync("Custom text to embed");
        Console.WriteLine($"Embedding dimension: {embedder.Dimension}");

        // Use vector store directly for custom operations
        var customMetadata = new Dictionary<string, object>
        {
            ["custom_field"] = "custom_value",
            ["text"] = "This is custom text"
        };
        
        await vectorStore.UpsertAsync("custom_id", customEmbedding, customMetadata);

        // Search with custom parameters
        var customResults = await vectorStore.SearchAsync(customEmbedding, 1);
        foreach (var result in customResults)
        {
            Console.WriteLine($"Custom result: {result.Key}, Score: {result.Score}");
        }
    }
}

/// <summary>
/// Comparison: Original vs Factory approach
/// 
/// ORIGINAL APPROACH (~300+ lines):
/// - Manual embedder creation
/// - Manual vector store creation
/// - Manual database management
/// - Manual ingestion engine setup
/// - Manual configuration validation
/// - Manual error handling
/// - Manual metadata management
/// - Complex initialization logic
/// 
/// FACTORY APPROACH (~20 lines):
/// - Simple configuration object
/// - One-line RAG system creation
/// - One-line directory ingestion
/// - Built-in error handling
/// - Automatic metadata management
/// - Validation handled by factory
/// - Clean resource disposal
/// - Sensible defaults with customization options
/// </summary>
public static class ComparisonExample
{
    public static async Task OriginalApproach()
    {
        // This would be ~300 lines of complex setup code...
        // See the original Program.cs for the full complexity
    }

    public static async Task FactoryApproach()
    {
        var config = new RAGConfiguration
        {
            EmbeddingModelPath = @"C:\RAGamuffin\model.onnx",
            EmbeddingTokenizerPath = @"C:\RAGamuffin\tokenizer.json",
            DatabaseDirectory = @"C:\RAGamuffin\",
            RecreateDatabase = true
        };

        using var ragSystem = RAGFactory.CreateRAGSystem(config);
        await ragSystem.IngestDirectoryAsync(@"C:\RAGamuffin\training-files\");
        var results = await ragSystem.SearchAsync("What are the paid holidays?");
        var context = SimpleRAGSystem.ExtractContextFromResults(results);
        
        // Use context with your LLM of choice...
    }
} 