using InstructSharp.Clients.ChatGPT;
using InstructSharp.Core;
using RAGamuffin.Abstractions;
using RAGamuffin.Common;
using RAGamuffin.Embedding;
using RAGamuffin.Ingestion.Engines;
using RAGamuffin.Models;
using RAGamuffin.VectorStores.Providers;

namespace RAGamuffin.Examples.PDFExtractor;

/// <summary>
/// RAGamuffin PDF Extractor Example
/// 
/// This example demonstrates how to use RAGamuffin with InstructSharp to create
/// a powerful RAG (Retrieval-Augmented Generation) system that can:
/// 1. Ingest and process PDF documents
/// 2. Create vector embeddings for semantic search
/// 3. Store vectors in a SQLite database
/// 4. Perform semantic search queries
/// 5. Use InstructSharp to generate AI-powered responses
///
/// </summary>
public class Program
{
    // ============================================================================
    // CONFIGURATION CONSTANTS
    // ============================================================================
    
    /// <summary>
    /// Directory containing training PDF files to ingest
    /// </summary>
    private const string TRAINING_FILES_DIRECTORY = @"C:\RAGamuffin\training-files\";
    
    /// <summary>
    /// Directory for storing the vector database
    /// </summary>
    private const string DATABASE_DIRECTORY = @"C:\RAGamuffin\";
    
    /// <summary>
    /// Name of the SQLite database file
    /// </summary>
    private const string DATABASE_NAME = "ragamuffin_documents.db";
    
    /// <summary>
    /// Name of the vector collection in the database
    /// </summary>
    private const string COLLECTION_NAME = "documents";
    
    /// <summary>
    /// InstructSharp API key for LLM integration
    /// </summary>
    private const string API_KEY = "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";

    /// <summary>
    /// Path to the ONNX embedding model
    /// For now we recommend https://huggingface.co/sentence-transformers/all-mpnet-base-v2/blob/main/onnx/model.onnx
    /// </summary>
    private const string EMBEDDING_MODEL_PATH = @"C:\RAGamuffin\model.onnx";

    /// <summary>
    /// Path to the embedding tokenizer
    /// For now we recommend https://huggingface.co/sentence-transformers/all-mpnet-base-v2/blob/main/tokenizer.json
    /// </summary>
    private const string EMBEDDING_TOKENIZER_PATH = @"C:\RAGamuffin\tokenizer.json";
    
    /// <summary>
    /// Flag to control whether to retrain the vector database
    /// Set to true to rebuild the database from scratch
    /// </summary>
    private const bool RETRAIN_DATABASE = true;
    
    /// <summary>
    /// Number of search results to retrieve
    /// </summary>
    private const int SEARCH_RESULT_COUNT = 5;
    
    /// <summary>
    /// Chunk size for document processing (in characters)
    /// </summary>
    private const int CHUNK_SIZE = 1200;
    
    /// <summary>
    /// Overlap between chunks (in characters)
    /// </summary>
    private const int CHUNK_OVERLAP = 500;

    // ============================================================================
    // MAIN EXECUTION
    // ============================================================================
    
    public static async Task Main(string[] args)
    {
        Console.WriteLine(">>> RAGamuffin PDF Extractor Example");
        Console.WriteLine("=====================================\n");
        
        try
        {
            // Initialize the RAG system
            var ragSystem = new RAGSystem();
            
            // Process documents and build vector database
            if (RETRAIN_DATABASE)
            {
                Console.WriteLine("[TRAIN] Training Mode: Building vector database...");
                await ragSystem.TrainDocumentsAsync();
                Console.WriteLine("[OK] Vector database built successfully!\n");
            }
            
            // Perform a semantic search query
            Console.WriteLine("[SEARCH] Performing semantic search...");

            // Example search query
            string searchQuery = "What are the paid holidays at this company?";

            var searchResults = await ragSystem.SearchAsync(searchQuery, SEARCH_RESULT_COUNT);
            
            // Generate AI response using InstructSharp
            Console.WriteLine("[AI] Generating AI response with InstructSharp...");
            var aiResponse = await ragSystem.GenerateResponseAsync(searchQuery, searchResults);
            
            // Display results
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
    // RAG SYSTEM CLASS
    // ============================================================================
    
    /// <summary>
    /// Main RAG system that orchestrates document ingestion, vector storage,
    /// semantic search, and AI response generation using InstructSharp.
    /// </summary>
    public class RAGSystem
    {
        private readonly IEmbedder _embedder;
        private IVectorStore _vectorStore;
        private readonly Dictionary<string, IIngestionEngine> _engines;
        private readonly ChatGPTClient _llmClient;
        private readonly string _fullDbPath;

        public RAGSystem()
        {
            // Initialize embedding model
            _embedder = new OnnxEmbedder(EMBEDDING_MODEL_PATH, EMBEDDING_TOKENIZER_PATH);
            
            // Store the database path but don't create vector store yet
            _fullDbPath = Path.Combine(DATABASE_DIRECTORY, DATABASE_NAME);
            
            // Initialize document ingestion engines
            _engines = new Dictionary<string, IIngestionEngine>
            {
                ["pdf"] = new PdfIngestionEngine(),
                ["text"] = new TextIngestionEngine()
            };
            
            // Initialize InstructSharp client
            _llmClient = new ChatGPTClient(API_KEY);
            
            Console.WriteLine("✅ RAG system initialized successfully");
        }

        /// <summary>
        /// Trains the system by ingesting documents from the training directory,
        /// creating vector embeddings, and storing them in the database.
        /// </summary>
        public async Task TrainDocumentsAsync()
        {
            // Handle database deletion BEFORE creating vector store
            if (RETRAIN_DATABASE)
            {
                try
                {
                    Console.WriteLine($">>> Deleting database: {_fullDbPath}");
                    DbHelper.DeleteSqliteDatabase(_fullDbPath);
                    Console.WriteLine($">>> Creating new database: {_fullDbPath}");
                    DbHelper.CreateSqliteDatabase(_fullDbPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING] Could not delete existing database: {ex.Message}");
                    Console.WriteLine("[INFO] Continuing with existing database...");
                }
            }
            
            // Now create the vector store (after database operations)
            if (_vectorStore == null)
            {
                _vectorStore = new SqliteVectorStoreProvider(_fullDbPath, COLLECTION_NAME);
            }
            
            // Get all PDF files from training directory
            var trainingFiles = GetTrainingFiles();
            Console.WriteLine($">>> Found {trainingFiles.Length} training files");
            
            // Configure ingestion options
            var pdfOptions = new PdfHybridParagraphIngestionOptions
            {
                MinSize = 0,  // Not used in fixed-size chunking
                MaxSize = CHUNK_SIZE,
                Overlap = CHUNK_OVERLAP,
                UseMetadata = true
            };
            
            var textOptions = new TextHybridParagraphIngestionOptions
            {
                MinSize = 0,  // Not used in fixed-size chunking
                MaxSize = CHUNK_SIZE,
                Overlap = CHUNK_OVERLAP,
                UseMetadata = true
            };
            
            // Process each file
            var ingestedItems = new List<IngestedItem>();
            foreach (var file in trainingFiles)
            {
                Console.WriteLine($">>> Processing: {Path.GetFileName(file)}");
                var extension = Path.GetExtension(file).ToLowerInvariant();
                
                List<IngestedItem> fileItems;
                switch (extension)
                {
                    case ".pdf":
                        fileItems = await _engines["pdf"].IngestAsync(file, pdfOptions, CancellationToken.None);
                        break;
                    default:
                        fileItems = await _engines["text"].IngestAsync(file, textOptions, CancellationToken.None);
                        break;
                }
                
                // Debug: Show chunk statistics
                if (fileItems.Any())
                {
                    var avgLength = fileItems.Average(item => item.Text.Length);
                    var minLength = fileItems.Min(item => item.Text.Length);
                    var maxLength = fileItems.Max(item => item.Text.Length);
                    Console.WriteLine($">>> Chunks: {fileItems.Count}, Avg: {avgLength:F0} chars, Min: {minLength}, Max: {maxLength}");
                }
                
                ingestedItems.AddRange(fileItems);
            }
            
            Console.WriteLine($">>>  Ingested {ingestedItems.Count} document chunks");
            
            // Create embeddings and store vectors
            Console.WriteLine(">>>  Creating vector embeddings...");
            foreach (var item in ingestedItems)
            {
                item.Vectors = await _embedder.EmbedAsync(item.Text);
                
                // Debug: Show what we're actually storing
                Console.WriteLine($"  Chunk {item.Id[..8]}... - Length: {item.Text.Length}");
                Console.WriteLine($"  Text preview: {(item.Text.Length > 100 ? item.Text[..100] + "..." : item.Text)}");
                
                // Create fresh metadata to ensure we have the correct text
                var metadata = new Dictionary<string, object>
                {
                    ["text"] = item.Text,  // Always use the full chunk text
                    ["Length"] = item.Text.Length,
                    ["source"] = item.Source,
                    ["id"] = item.Id
                };
                
                await _vectorStore.UpsertAsync(item.Id, item.Vectors, metadata);
            }
            
            Console.WriteLine($">>> Stored {ingestedItems.Count} vectors in database");
        }

        /// <summary>
        /// Performs semantic search using the query and returns the most relevant results.
        /// </summary>
        /// <param name="query">The search query</param>
        /// <param name="resultCount">Number of results to return</param>
        /// <returns>List of search results with scores and metadata</returns>
        public async Task<IEnumerable<(string Key, float Score, IDictionary<string, object> MetaData)>> SearchAsync(string query, int resultCount)
        {
            // Ensure vector store is created
            if (_vectorStore == null)
            {
                _vectorStore = new SqliteVectorStoreProvider(_fullDbPath, COLLECTION_NAME);
            }
            
            var queryVector = await _embedder.EmbedAsync(query);
            return await _vectorStore.SearchAsync(queryVector, resultCount);
        }

        /// <summary>
        /// Generates an AI response using InstructSharp based on the search results.
        /// </summary>
        /// <param name="query">The original user query</param>
        /// <param name="searchResults">The retrieved search results</param>
        /// <returns>The AI-generated response</returns>
        public async Task<string> GenerateResponseAsync(string query, IEnumerable<(string Key, float Score, IDictionary<string, object> MetaData)> searchResults)
        {
            // Extract context from search results
            var contextTexts = searchResults
                .Where(r => r.MetaData?.ContainsKey("text") == true)
                .Select(r => r.MetaData["text"].ToString())
                .Where(text => !string.IsNullOrEmpty(text))
                .ToList();
            
            var contextForLLM = string.Join("\n\n", contextTexts);
            
            // Create InstructSharp request
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
{contextForLLM}"
            };
            
            // Get response from InstructSharp
            var response = await _llmClient.QueryAsync<string>(request);
            return response.Result;
        }

        /// <summary>
        /// Gets all training files from the configured directory.
        /// </summary>
        /// <returns>Array of file paths</returns>
        private string[] GetTrainingFiles()
        {
            if (!Directory.Exists(TRAINING_FILES_DIRECTORY))
            {
                throw new DirectoryNotFoundException(
                    $"Training directory not found: {TRAINING_FILES_DIRECTORY}. " +
                    "Please create this directory and add your PDF files.");
            }
            
            return Directory.GetFiles(TRAINING_FILES_DIRECTORY, "*.pdf", SearchOption.AllDirectories);
        }
    }

    // ============================================================================
    // DISPLAY UTILITIES
    // ============================================================================
    
    /// <summary>
    /// Displays the search results and AI response in a formatted way.
    /// </summary>
    private static void DisplayResults(string query, IEnumerable<(string Key, float Score, IDictionary<string, object> MetaData)> searchResults, string aiResponse)
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
            
            // Debug: Show all available metadata keys
            if (result.MetaData != null)
            {
                Console.WriteLine($"  Available metadata keys: {string.Join(", ", result.MetaData.Keys)}");
                Console.WriteLine($"  Metadata count: {result.MetaData.Count}");
                
                // Show all metadata values for debugging
                foreach (var kvp in result.MetaData)
                {
                    var value = kvp.Value?.ToString() ?? "null";
                    var preview = value.Length > 100 ? value[..100] + "..." : value;
                    Console.WriteLine($"    {kvp.Key}: {preview}");
                }
                
                if (result.MetaData.ContainsKey("text"))
                {
                    var text = result.MetaData["text"].ToString() ?? "";
                    var preview = text.Length > 200 ? text[..200] + "..." : text;
                    Console.WriteLine($"  Text: {preview}");
                }
                else
                {
                    Console.WriteLine(">>> No 'text' key found in metadata");
                }
            }
            else
            {
                Console.WriteLine(">>>  No metadata available");
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