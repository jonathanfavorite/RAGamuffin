using RAGamuffin.Builders;
using RAGamuffin.Core;
using RAGamuffin.Embedding;
using RAGamuffin.Ingestion;
using InstructSharp;
using InstructSharp.Clients.ChatGPT;
using InstructSharp.Core;

namespace RAGamuffin.Examples.TrainAndSearch
{
    // RAGamuffin Example: Demonstrates how to build a simple RAG (Retrieval-Augmented Generation) pipeline
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
        // SQLite is the default database provider for simplicity
        // The database will be created automatically if it doesn't exist

        private const string DatabaseDirectory = @"C:\RAGamuffin\";
        private const string DatabaseName = "RAGamuffin_documents.db";
        private const string CollectionName = "handbook_documents";

        // ==================== TRAINING CONFIGURATION ====================

        // Set to true to drop existing data and retrain from scratch
        // For this demo, we will retrain the database each time
        // If it's set to false, it will run the ingestion engine but will not save the data to the database
        private const bool RetrainData = true;

        // Maximum number of relevant chunks to retrieve during search
        private const int MaxSearchResults = 5;

        static async Task Main()
        {
            // ============================================================
            //                    SETUP & INITIALIZATION
            // ============================================================

            var dbPath = Path.Combine(DatabaseDirectory, DatabaseName);

            // Define your training documents
            var trainingFiles = new[]
            {
                @"C:\RAGamuffin\training-files\company-handbook.pdf"
            };

            // ============================================================
            //                  BUILD INGESTION PIPELINE
            // ============================================================

            var pipeline = new IngestionTrainingBuilder()
                // Configure embedding model
                .WithEmbeddingModel(new OnnxEmbedder(EmbeddingModelPath, EmbeddingTokenizerPath))

                // Configure vector database
                .WithVectorDatabase(new SqliteDatabaseModel(dbPath, CollectionName, RetrainData))

                // Configure PDF processing options (Below are the defaults)
                .WithPdfOptions(new PdfHybridParagraphIngestionOptions
                {
                    MinSize = 0,        // Minimum chunk size
                    MaxSize = 800,     // Maximum chunk size
                    Overlap = 400,      // Overlap between chunks for context preservation
                    UseMetadata = true  // Include document metadata
                })

                // Configure text file processing options (Below are the defaults)
                .WithTextOptions(new TextHybridParagraphIngestionOptions
                {
                    MinSize = 500,      // Minimum chunk size
                    MaxSize = 800,     // Maximum chunk size
                    Overlap = 400,      // Overlap between chunks
                    UseMetadata = true  // Include document metadata
                })

                // Set training files and database behavior
                .WithTrainingFiles(trainingFiles)
                .DropDatabaseAndRetrain(RetrainData)
                .Build();

            // ============================================================
            //                    TRAIN THE PIPELINE
            // ============================================================

            Console.WriteLine("Starting document ingestion...");
            var ingestedItems = await pipeline.Train(trainingFiles);
            Console.WriteLine($"Successfully ingested {ingestedItems.Count()} document chunks\n");

            // ============================================================
            //                  PERFORM VECTOR SEARCH
            // ============================================================

            string searchQuery = "What are the company policies?";
            Console.WriteLine($"Searching for: {searchQuery}");

            // Execute vector search and retrieve relevant text chunks
            string[] vectorSearchResults = await pipeline.SearchAndReturnTexts(
                searchQuery,
                MaxSearchResults
            );

            Console.WriteLine($"Found {vectorSearchResults.Length} relevant chunks\n");

            // ============================================================
            //                    QUERY THE LLM
            // ============================================================

            Console.WriteLine("Querying LLM for response...");
            string llmResponse = await QueryLLM(
                searchQuery,
                string.Join("\n\n", vectorSearchResults)
            );

            Console.WriteLine("\nLLM Response:\n");
            Console.WriteLine(llmResponse);
        }

        // Queries an LLM with the user's question and relevant context from vector search
        // Parameters:
        //   query: User's original question
        //   context: Retrieved context from vector search
        //   cancellationToken: Cancellation token for async operations
        // Returns: LLM's response based on the provided context
        static async Task<string> QueryLLM(string query, string context, CancellationToken cancellationToken = default)
        {
            // ============================================================
            //                  LLM SYSTEM INSTRUCTIONS
            // ============================================================

            string systemInstructions = @"
                You are a helpful assistant. Answer the question based on the provided context.
                
                Guidelines:
                • Only use information from the provided context to eliminate false information
                • Be direct and factual in your responses
                • If referencing the context, use natural language like 'according to my training data'
                • Provide literal information from the context when possible
                • If the context doesn't contain relevant information, clearly state that
            ".Trim();

            // ============================================================
            //                  FORMAT MODEL INPUT
            // ============================================================

            string modelInput = $"""
                User Question: {query}
                
                Relevant Context:
                {context}
                """;

            // ============================================================
            //               INITIALIZE CHATGPT CLIENT
            // ============================================================
            // Using InstructSharp - another package maintained by the author
            // Learn more at: https://github.com/jonathanfavorite/InstructSharp

            ChatGPTClient client = new("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");

            ChatGPTRequest request = new()
            {
                Model = ChatGPTModels.GPT4o,
                Instructions = systemInstructions,
                Input = modelInput
            };

            // ============================================================
            //                   EXECUTE LLM QUERY
            // ============================================================

            LLMResponse<string> response = await client.QueryAsync<string>(request);

            return response.Result ?? "No response from LLM. Please check your API key and model settings.";
        }
    }
}