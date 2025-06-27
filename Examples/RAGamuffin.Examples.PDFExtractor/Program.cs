using InstructSharp.Clients.ChatGPT;
using RAGamuffin.Factories;

namespace RAGamuffin.Examples.PDFExtractor;

public class Program
{
    // ============================================================================
    // SIMPLIFIED CONFIGURATION
    // ============================================================================
    
    /// <summary>
    /// Base directory containing all RAG files (model, tokenizer, database)
    /// </summary>
    private const string BASE_DIRECTORY = @"C:\RAGamuffin\";
    
    /// <summary>
    /// Directory containing training PDF files to ingest
    /// </summary>
    private const string TRAINING_FILES_DIRECTORY = @"C:\RAGamuffin\training-files\";
    
    /// <summary>
    /// InstructSharp API key for LLM integration
    /// </summary>
    private const string API_KEY = "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";
    
    /// <summary>
    /// Whether to recreate the database (set to true for fresh start)
    /// </summary>
    private const bool RECREATE_DATABASE = false;
    
    // ============================================================================
    // CHUNKING STRATEGY EXAMPLES - Choose based on your document type and use case
    // ============================================================================
    
    /// <summary>
    /// Different chunking strategies for different scenarios:
    /// 
    /// SMALL CHUNKS (400-600 chars, 100-200 overlap):
    /// - Best for: FAQ documents, short-form content, Q&A systems
    /// - Pros: Precise retrieval, less noise
    /// - Cons: May lose broader context
    /// 
    /// MEDIUM CHUNKS (800-1200 chars, 200-400 overlap):
    /// - Best for: General documents, articles, most use cases
    /// - Pros: Good balance of precision and context
    /// - Cons: Balanced approach - no major drawbacks
    /// 
    /// LARGE CHUNKS (1500-2000 chars, 500-800 overlap):
    /// - Best for: Legal documents, academic papers, complex technical docs
    /// - Pros: Preserves complex context and relationships
    /// - Cons: May include some irrelevant information
    /// 
    /// CURRENT EXAMPLE: Using MEDIUM chunks (1200/500) - good for most PDF documents
    /// </summary>
    public static class ChunkingStrategies
    {
        // Small chunks for FAQ-style content
        public static (int size, int overlap) SmallChunks => (500, 150);
        
        // Medium chunks for general documents (default)
        public static (int size, int overlap) MediumChunks => (1200, 500);
        
        // Large chunks for complex documents
        public static (int size, int overlap) LargeChunks => (1800, 700);
        
        // Custom chunks - adjust these values for your specific needs
        public static (int size, int overlap) CustomChunks => (1000, 300);
    }

    // ============================================================================
    // MAIN EXECUTION - DRAMATICALLY SIMPLIFIED!
    // ============================================================================
    
    public static async Task Main(string[] args)
    {
        Console.WriteLine(">>> RAGamuffin PDF Extractor Example (Simplified Version)");
        Console.WriteLine("==========================================================\n");
        
        try
        {
            // ==========================================
            // STEP 1: Create RAG system with FINE CONTROL over chunking
            // ==========================================
            
            Console.WriteLine("[SETUP] Creating RAG system with custom chunking configuration...");
            
            // Fine control over chunking parameters - customize as needed!
            // Choose your chunking strategy based on document type:
            var (chunkSize, overlap) = ChunkingStrategies.MediumChunks; // Change this to try different strategies!
            
            // Alternative ways to configure chunking:
            // var (chunkSize, overlap) = ChunkingStrategies.SmallChunks;  // For FAQ/Q&A content
            // var (chunkSize, overlap) = ChunkingStrategies.LargeChunks;  // For complex technical documents
            // var (chunkSize, overlap) = (800, 200);                     // Custom values
            // var (chunkSize, overlap) = (2000, 1000);                   // Very large chunks for academic papers
            
            // Note: Using .Create() instead of .CreateForDevelopment() to avoid default overrides
            // This ensures RECREATE_DATABASE constant is respected exactly as set
            
            using var ragSystem = RAGBuilder
                .Create()  // Start with blank configuration
                .WithEmbedding(
                    Path.Combine(BASE_DIRECTORY, "model.onnx"),
                    Path.Combine(BASE_DIRECTORY, "tokenizer.json"))
                .WithDatabase(BASE_DIRECTORY, "ragamuffin_documents.db", "documents")
                .WithDatabaseRecreation(RECREATE_DATABASE)  // Explicit control over database recreation
                .WithChunking(
                    chunkSize: chunkSize,    // Fine control: Adjust chunk size for your content
                    overlap: overlap)        // Fine control: Adjust overlap for better context retention
                .WithDefaultSearchResults(5)
                .WithMetadata(true)
                .BuildSystem();
            
            // Display the configuration being used (including database recreation setting)
            var config = ragSystem.GetConfiguration();
            Console.WriteLine($"📊 RAG System Configuration:");
            Console.WriteLine($"   • Database Path: {config.FullDatabasePath}");
            Console.WriteLine($"   • Recreate Database: {config.RecreateDatabase} (Constant value: {RECREATE_DATABASE})");
            Console.WriteLine($"   • Collection Name: {config.CollectionName}");
            Console.WriteLine();
            Console.WriteLine($"📏 Chunking Configuration:");
            Console.WriteLine($"   • Chunk Size: {config.ChunkSize} characters");
            Console.WriteLine($"   • Chunk Overlap: {config.ChunkOverlap} characters");
            Console.WriteLine($"   • Effective chunk step: {config.ChunkSize - config.ChunkOverlap} characters");
            Console.WriteLine($"   • Overlap percentage: {(double)config.ChunkOverlap / config.ChunkSize * 100:F1}%");
            Console.WriteLine($"   • Strategy: {GetChunkingStrategyName(config.ChunkSize, config.ChunkOverlap)}");
            Console.WriteLine();
            Console.WriteLine("💡 Chunking Impact:");
            Console.WriteLine("   • Larger chunks = Better context retention, but potentially more noise");
            Console.WriteLine("   • Smaller chunks = More precise retrieval, but may lose broader context");
            Console.WriteLine("   • Higher overlap = Better context continuity, but more storage needed");
            Console.WriteLine("   • Lower overlap = More efficient storage, but potential context gaps");
            
            Console.WriteLine("✅ RAG system created successfully!\n");

            // ==========================================
            // STEP 2: Ingest documents
            // ==========================================
            
            Console.WriteLine("[TRAIN] Ingesting PDF documents...");
            
            var chunksIngested = await ragSystem.IngestDirectoryAsync(TRAINING_FILES_DIRECTORY, "*.pdf");
            
            Console.WriteLine($"✅ Successfully ingested {chunksIngested} chunks from PDF files!\n");

            // ==========================================
            // STEP 3: Perform semantic search
            // ==========================================
            
            Console.WriteLine("[SEARCH] Performing semantic search...");
            
            string searchQuery = "What are the paid holidays at this company?";
            
            // This single line replaces ~20 lines of search setup code!
            var searchResults = await ragSystem.SearchAsync(searchQuery, resultCount: 5);
            
            Console.WriteLine($"!!! Found {searchResults.Count()} relevant results!\n");

            // ==========================================
            // STEP 4: Generate AI response with InstructSharp
            // ==========================================
            
            Console.WriteLine("[AI] Generating AI response with InstructSharp...");
            
            // Extract context for LLM (utility method provided by factory)
            var context = SimpleRAGSystem.ExtractContextFromResults(searchResults);
            
            // Generate response using InstructSharp (same as before)
            var aiResponse = await GenerateInstructSharpResponse(searchQuery, context);
            
            Console.WriteLine("!!! AI response generated successfully!\n");

            // ==========================================
            // STEP 5: Display results
            // ==========================================
            
            DisplayResults(searchQuery, searchResults, aiResponse);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    // ============================================================================
    // HELPER METHODS
    // ============================================================================
    
    /// <summary>
    /// Identifies which chunking strategy is being used based on size and overlap
    /// </summary>
    private static string GetChunkingStrategyName(int chunkSize, int overlap)
    {
        var strategies = new[]
        {
            ("Small Chunks", ChunkingStrategies.SmallChunks),
            ("Medium Chunks", ChunkingStrategies.MediumChunks),
            ("Large Chunks", ChunkingStrategies.LargeChunks),
            ("Custom Chunks", ChunkingStrategies.CustomChunks)
        };
        
        foreach (var (name, (size, overlp)) in strategies)
        {
            if (chunkSize == size && overlap == overlp)
                return name;
        }
        
        return $"Custom ({chunkSize}/{overlap})";
    }
    
    /// <summary>
    /// Generates an AI response using InstructSharp (same as before, but simpler setup)
    /// </summary>
    private static async Task<string> GenerateInstructSharpResponse(string query, string context)
    {
        var llmClient = new ChatGPTClient(API_KEY);
        
        var request = new ChatGPTRequest
        {
            Model = ChatGPTModels.GPT4o,

            Instructions = @"You are a helpful assistant. Answer the question based on the provided context. 
                           Only refer to the context provided to eliminate false information. 
                           Try not to mention context, if you _have_ to mention it, say something like 
                           'according to my training data' or something. 
                           !!!Important: try and provide the literal information provided from the context.",
            
            Input = $@"Here is the user's question: {query}

                    And here is the context I have found:
                    {context}"
            };
        
        var response = await llmClient.QueryAsync<string>(request);
        return response.Result;
    }

    /// <summary>
    /// Displays the search results and AI response (simplified version)
    /// </summary>
    private static void DisplayResults(
        string query, 
        IEnumerable<(string Key, float Score, IDictionary<string, object> MetaData)> searchResults, 
        string aiResponse)
    {
        Console.WriteLine("\n" + "=".PadRight(80, '='));
        Console.WriteLine(">>> SEARCH RESULTS");
        Console.WriteLine("=".PadRight(80, '='));
        
        Console.WriteLine($"Query: {query}\n");
        
        int resultIndex = 1;
        foreach (var result in searchResults)
        {
            Console.WriteLine($"Result {resultIndex}:");
            Console.WriteLine($"  Score: {result.Score:F4}");
            Console.WriteLine($"  ID: {result.Key}");
            
            if (result.MetaData?.ContainsKey("text") == true)
            {
                var text = result.MetaData["text"].ToString() ?? "";
                var preview = text.Length > 200 ? text[..200] + "..." : text;
                Console.WriteLine($"  Text: {preview}");
            }
            
            Console.WriteLine();
            resultIndex++;
        }
        
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine(">>> AI RESPONSE");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine(aiResponse);
        Console.WriteLine("=".PadRight(80, '='));
    }
}