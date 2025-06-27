using RAGamuffin.Factories;

namespace RAGamuffin.Examples;

/// <summary>
/// Ultimate simplification examples showing the power of the factory system
/// </summary>
public static class UltimateSimplification
{
    /// <summary>
    /// THE SIMPLEST POSSIBLE RAG SETUP (1 line!)
    /// This replaces the entire 300+ line original example
    /// </summary>
    public static async Task OneLiner()
    {
        // This one line replaces ALL the complex setup from the original example!
        using var rag = RAGBuilder.QuickStart(@"C:\RAGamuffin\");
        
        // That's it! You now have a fully functional RAG system.
        // The factory handles:
        // - Embedder creation and configuration
        // - Vector store creation and database setup
        // - Ingestion engine configuration
        // - Error handling and validation
        // - Resource management
        // - Sensible defaults for all parameters
    }

    /// <summary>
    /// SIMPLE USAGE PATTERN (5 lines total)
    /// Complete RAG pipeline from setup to search
    /// </summary>
    public static async Task<string> FiveLineRAG()
    {
        using var rag = RAGBuilder.QuickStart(@"C:\RAGamuffin\");
        await rag.IngestDirectoryAsync(@"C:\RAGamuffin\training-files\");
        var results = await rag.SearchAsync("What are the paid holidays?");
        var context = SimpleRAGSystem.ExtractContextFromResults(results);
        return context; // Use with your LLM
    }

    /// <summary>
    /// FLUENT BUILDER PATTERN (readable and flexible)
    /// </summary>
    public static async Task<string> FluentBuilder()
    {
        using var rag = RAGBuilder
            .Create()
            .WithEmbedding(@"C:\RAGamuffin\model.onnx", @"C:\RAGamuffin\tokenizer.json")
            .WithDatabase(@"C:\RAGamuffin\", "my_docs.db")
            .WithChunking(chunkSize: 1000, overlap: 300)
            .BuildSystem();

        await rag.IngestDirectoryAsync(@"C:\docs\");
        var results = await rag.SearchAsync("How do I submit expenses?");
        return SimpleRAGSystem.ExtractContextFromResults(results);
    }

    /// <summary>
    /// PRODUCTION-READY SETUP (optimized for production use)
    /// </summary>
    public static async Task<string> ProductionSetup()
    {
        using var rag = RAGBuilder
            .CreateForProduction(@"C:\RAGamuffin\")
            .WithDatabase(@"C:\RAGamuffin\", "production_vectors.db", "company_docs")
            .BuildSystem();

        // Ingest multiple document types
        await rag.IngestDirectoryAsync(@"C:\company-docs\policies\", "*.pdf");
        await rag.IngestDirectoryAsync(@"C:\company-docs\handbooks\", "*.txt");
        
        var results = await rag.SearchAsync("What is the remote work policy?", resultCount: 10);
        return SimpleRAGSystem.ExtractContextFromResults(results);
    }

    /// <summary>
    /// DEVELOPMENT/TESTING SETUP (optimized for development)
    /// </summary>
    public static async Task<string> DevelopmentSetup()
    {
        using var rag = RAGBuilder
            .CreateForDevelopment(@"C:\RAGamuffin\")
            .WithChunking(500, 100) // Smaller chunks for testing
            .BuildSystem();

        await rag.IngestFileAsync(@"C:\test-docs\sample.pdf");
        var results = await rag.SearchAsync("test query", resultCount: 3);
        return SimpleRAGSystem.ExtractContextFromResults(results);
    }

    /// <summary>
    /// COMPARISON: Original vs New Approach
    /// </summary>
    public static class ComparisonSummary
    {
        public static void ShowComparison()
        {
            /*
            ORIGINAL APPROACH (from the example):
            =====================================
            
            ✗ ~300+ lines of complex setup code
            ✗ Manual embedder creation with complex parameters
            ✗ Manual vector store creation and database management
            ✗ Manual ingestion engine setup for each file type
            ✗ Complex error handling and resource management
            ✗ Manual metadata configuration
            ✗ Hard-coded configuration constants
            ✗ Complex initialization order dependencies
            ✗ Manual database recreation logic
            ✗ Verbose search result processing
            ✗ No built-in validation
            ✗ Difficult to customize without breaking things
            ✗ Hard to test different configurations
            ✗ Lots of repetitive boilerplate code
            ✗ Error-prone setup process
            
            NEW FACTORY APPROACH:        
            ======================
            
            ✓ 1 line for basic setup: RAGBuilder.QuickStart(@"C:\RAGamuffin\")
            ✓ 5 lines for complete RAG pipeline (setup + ingest + search)
            ✓ Automatic component creation and wiring
            ✓ Built-in error handling and validation
            ✓ Sensible defaults with easy customization
            ✓ Fluent builder pattern for readability
            ✓ Production and development presets
            ✓ Automatic resource management (IDisposable)
            ✓ Type-safe configuration
            ✓ Easy to test and mock
            ✓ Extensible for future enhancements
            ✓ Self-documenting through method names
            ✓ Reduced chance of configuration errors
            ✓ Easy to switch between different setups
            ✓ Clean separation of concerns
            
            LINES OF CODE COMPARISON:
            =========================
            Original: ~300+ lines
            New:      1-5 lines for most scenarios
            Reduction: 98%+ code reduction while maintaining full functionality!
            */
        }
    }

    /// <summary>
    /// ADVANCED CUSTOMIZATION (when you need more control)
    /// Shows that flexibility is still maintained
    /// </summary>
    public static async Task AdvancedCustomization()
    {
        // Even advanced scenarios are much simpler
        var config = RAGBuilder
            .Create()
            .WithEmbedding(@"C:\models\custom-model.onnx", @"C:\models\custom-tokenizer.json")
            .WithDatabase(@"C:\databases\", "specialized.db", "legal_documents")
            .WithChunking(chunkSize: 2000, overlap: 800) // Larger chunks for legal docs
            .WithMetadata(true)
            .WithDefaultSearchResults(15)
            .Build();

        using var rag = RAGFactory.CreateRAGSystem(config);
        
        // Still get all the benefits with custom configuration
        await rag.IngestDirectoryAsync(@"C:\legal-docs\", "*.pdf");
        var results = await rag.SearchAsync("contract termination clauses", resultCount: 20);
        
        // Access underlying components if needed for advanced scenarios
        var embedder = rag.GetEmbedder();
        var vectorStore = rag.GetVectorStore();
        
        // Custom embedding
        var customVector = await embedder.EmbedAsync("custom text");
        
        // Custom metadata
        var customMetadata = new Dictionary<string, object>
        {
            ["document_type"] = "legal",
            ["classification"] = "confidential",
            ["text"] = "custom document text"
        };
        
        await vectorStore.UpsertAsync("custom_doc_1", customVector, customMetadata);
    }
}

/// <summary>
/// Real-world usage examples for different scenarios
/// </summary>
public static class RealWorldExamples
{
    /// <summary>
    /// Customer support knowledge base
    /// </summary>
    public static async Task CustomerSupportKB()
    {
        using var rag = RAGBuilder
            .CreateForProduction(@"C:\support-kb\")
            .WithDatabase(@"C:\support-kb\", "support_kb.db", "articles")
            .WithChunking(800, 200) // Good for FAQ-style content
            .BuildSystem();

        await rag.IngestDirectoryAsync(@"C:\support-docs\faqs\");
        await rag.IngestDirectoryAsync(@"C:\support-docs\manuals\");
        
        var customerQuery = "How do I reset my password?";
        var results = await rag.SearchAsync(customerQuery, resultCount: 3);
        var context = SimpleRAGSystem.ExtractContextFromResults(results);
        
        // Send context to your chat bot or support system
        // var response = await ChatBot.GenerateResponse(customerQuery, context);
    }

    /// <summary>
    /// Legal document analysis
    /// </summary>
    public static async Task LegalDocumentAnalysis()
    {
        using var rag = RAGBuilder
            .CreateForProduction(@"C:\legal-rag\")
            .WithDatabase(@"C:\legal-rag\", "legal_docs.db", "contracts")
            .WithChunking(1500, 600) // Larger chunks for legal context
            .WithDefaultSearchResults(10)
            .BuildSystem();

        await rag.IngestDirectoryAsync(@"C:\contracts\", "*.pdf");
        
        var legalQuery = "What are the liability limitations in software contracts?";
        var results = await rag.SearchAsync(legalQuery);
        var context = SimpleRAGSystem.ExtractContextFromResults(results);
        
        // Use with legal AI assistant
        // var analysis = await LegalAI.AnalyzeContracts(legalQuery, context);
    }

    /// <summary>
    /// Research paper analysis
    /// </summary>  
    public static async Task ResearchPaperAnalysis()
    {
        using var rag = RAGBuilder
            .CreateForProduction(@"C:\research-rag\")
            .WithDatabase(@"C:\research-rag\", "papers.db", "academic_papers")
            .WithChunking(1000, 400) // Good for academic content
            .BuildSystem();

        await rag.IngestDirectoryAsync(@"C:\research-papers\", "*.pdf");
        
        var researchQuery = "What are the latest developments in transformer architectures?";
        var results = await rag.SearchAsync(researchQuery, resultCount: 15);
        var context = SimpleRAGSystem.ExtractContextFromResults(results);
        
        // Use with research AI assistant
        // var summary = await ResearchAI.SummarizePapers(researchQuery, context);
    }
} 