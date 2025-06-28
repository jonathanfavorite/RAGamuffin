using RAGamuffin.Embedding;
using RAGamuffin.VectorStores;
using InstructSharp.Clients.ChatGPT;
using InstructSharp.Core;

namespace RAGamuffin.Examples.SearchExistingVectorStore
{
    // RAGamuffin Example: Demonstrates how to search an existing vector store
    // without using the full ingestion pipeline, including metadata retrieval
    static class Program
    {
        // ==================== EMBEDDING MODEL CONFIGURATION ====================
        // Recommended starter configuration:
        // • Type: ONNX (for cross-platform compatibility)
        // • Model: all-mpnet-base-v2 from HuggingFace
        //   Download from: https://huggingface.co/sentence-transformers/all-mpnet-base-v2/blob/main/onnx/model.onnx
        // • Tokenizer: Matching tokenizer for the model
        //   Download from: https://huggingface.co/sentence-transformers/all-mpnet-base-v2/resolve/main/tokenizer.json
        private const string EmbeddingModelPath = @"C:\RAGamuffin\model.onnx";
        private const string EmbeddingTokenizerPath = @"C:\RAGamuffin\tokenizer.json";

        // ==================== DATABASE CONFIGURATION ====================
        // Point to the existing database created by TrainAndSearch
        private const string DatabaseDirectory = @"C:\RAGamuffin\";
        private const string DatabaseName = "RAGamuffin_documents.db";
        private const string CollectionName = "handbook_documents";

        // ==================== SEARCH CONFIGURATION ====================
        private const int MaxSearchResults = 5;

        static async Task Main()
        {
            // ============================================================
            //                    SETUP & INITIALIZATION
            // ============================================================

            var dbPath = Path.Combine(DatabaseDirectory, DatabaseName);

            // Check if the database exists
            if (!File.Exists(dbPath))
            {
                Console.WriteLine($"Database not found at: {dbPath}");
                Console.WriteLine("Please run the TrainAndSearch example first to create the database.");
                return;
            }

            Console.WriteLine($"Using existing database: {dbPath}");

            // ============================================================
            //                  INITIALIZE COMPONENTS
            // ============================================================

            // Initialize the embedding model
            var embedder = new OnnxEmbedder(EmbeddingModelPath, EmbeddingTokenizerPath);
            Console.WriteLine("Embedding model initialized");

            // Initialize the vector store provider (don't retrain - use existing data)
            var vectorStore = new SqliteVectorStoreProvider(dbPath, CollectionName);
            Console.WriteLine("Vector store provider initialized");

            // ============================================================
            //                  METADATA RETRIEVAL DEMO
            // ============================================================

            Console.WriteLine("\n=== Metadata Retrieval Demo ===");
            
            // Get document count
            var documentCount = await vectorStore.GetDocumentCountAsync();
            Console.WriteLine($"Total documents in store: {documentCount}");

            // Get all document metadata
            var allMetadata = await vectorStore.GetAllDocumentsMetadataAsync();
            Console.WriteLine($"\nRetrieved metadata for {allMetadata.Count()} documents");

            // Show sample metadata from first few documents
            var sampleDocs = allMetadata.Take(3);
            foreach (var doc in sampleDocs)
            {
                Console.WriteLine($"\nDocument: {doc.DocumentId}");
                if (doc.Metadata != null)
                {
                    foreach (var kvp in doc.Metadata)
                    {
                        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }
            }

            // ============================================================
            //                  PERFORM VECTOR SEARCH
            // ============================================================

            string searchQuery = "What are the paid holidays/days off we get?";

            Console.WriteLine($"\n=== Vector Search Demo ===");
            Console.WriteLine($"Searching for: {searchQuery}");

            // Execute vector search with metadata
            var searchResults = await vectorStore.SearchAsync(
                searchQuery,
                embedder,
                MaxSearchResults
            );

            Console.WriteLine($"Found {searchResults.Count()} relevant chunks\n");

            // Display search results with metadata
            foreach (var result in searchResults)
            {
                Console.WriteLine($"Score: {result.Score:F3}");
                Console.WriteLine($"Document ID: {result.Key}");
                
                if (result.MetaData != null)
                {
                    Console.WriteLine("Metadata:");
                    foreach (var kvp in result.MetaData)
                    {
                        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }
                Console.WriteLine();
            }

            // ============================================================
            //                    QUERY THE LLM
            // ============================================================

            Console.WriteLine("=== LLM Query Demo ===");
            Console.WriteLine("Querying LLM for response...");
            
            // Get just the text content for LLM
            string[] vectorSearchTexts = await vectorStore.SearchAndReturnTexts(
                searchQuery,
                embedder,
                MaxSearchResults
            );
            
            string llmResponse = await QueryLLM(searchQuery, string.Join("\n\n", vectorSearchTexts));

            Console.WriteLine("\nLLM Response:\n");
            Console.WriteLine(llmResponse);
        }

        // Queries an LLM with the user's question and relevant context from vector search
        static async Task<string> QueryLLM(string query, string context, CancellationToken cancellationToken = default)
        {
            string systemInstructions = @"
                You are a helpful assistant. Answer the question based on the provided context.
                
                Guidelines:
                • Only use information from the provided context to eliminate false information
                • Be direct and factual in your responses
                • If referencing the context, use natural language like 'according to my training data'
                • Provide literal information from the context when possible
                • If the context doesn't contain relevant information, clearly state that
            ".Trim();

            string modelInput = $"""
                User Question: {query}
                
                Relevant Context:
                {context}
                """;

            ChatGPTClient client = new("YOUR-OPENAI-API-KEY");

            ChatGPTRequest request = new()
            {
                Model = ChatGPTModels.GPT4o,
                Instructions = systemInstructions,
                Input = modelInput
            };

            LLMResponse<string> response = await client.QueryAsync<string>(request);

            return response.Result ?? "No response from LLM. Please check your API key and model settings.";
        }
    }
} 