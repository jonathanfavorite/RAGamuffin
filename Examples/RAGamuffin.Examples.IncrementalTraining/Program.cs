using RAGamuffin.Builders;
using RAGamuffin.Core;
using RAGamuffin.Embedding;
using RAGamuffin.Enums;
using InstructSharp.Clients.ChatGPT;
using InstructSharp.Core;
using RAGamuffin.Ingestion;

namespace RAGamuffin.Examples.IncrementalTraining
{
    // RAGamuffin Example: Demonstrates adding more documents to an existing vector store
    static class Program
    {
        // ==================== EMBEDDING MODEL CONFIGURATION ====================
        private const string EmbeddingModelPath = @"C:\RAGamuffin\model.onnx";
        private const string EmbeddingTokenizerPath = @"C:\RAGamuffin\tokenizer.json";

        // ==================== DATABASE CONFIGURATION ====================
        private const string DatabaseDirectory = @"C:\RAGamuffin\";
        private const string DatabaseName = "RAGamuffin_documents.db";
        private const string CollectionName = "handbook_documents";

        // ==================== TRAINING CONFIGURATION ====================
        private const int MaxSearchResults = 5;

        static async Task Main()
        {
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

            var embedder = new OnnxEmbedder(EmbeddingModelPath, EmbeddingTokenizerPath);
            Console.WriteLine("Embedding model initialized");

            // ============================================================
            //                  CHECK CURRENT STATE
            // ============================================================

            var pipeline = IngestionTrainingBuilder.CreateForStateManagement(
                embedder, 
                new SqliteDatabaseModel(dbPath, CollectionName)
            ).Build();

            var currentCount = await pipeline.GetDocumentCount();
            Console.WriteLine($"Current document count: {currentCount}");

            // ============================================================
            //                  ADD DOCUMENTS DEMO
            // ============================================================

            Console.WriteLine("\n=== ADD DOCUMENTS DEMO ===");
            
            var documentsToAdd = new[]
            {
                @"C:\RAGamuffin\training-files\hippa-confidentiality.pdf"
            };

            // Check which files exist
            var existingFiles = documentsToAdd.Where(File.Exists).ToArray();
            if (existingFiles.Length == 0)
            {
                Console.WriteLine("No new training files found. Using dummy files for demonstration.");
            }

            var addPipeline = new IngestionTrainingBuilder()
                .WithEmbeddingModel(embedder)
                .WithVectorDatabase(new SqliteDatabaseModel(dbPath, CollectionName))
                .WithTrainingStrategy(TrainingStrategy.IncrementalAdd)
                .WithPdfOptions(new PdfHybridParagraphIngestionOptions
                {
                    MinSize = 0,
                    MaxSize = 800,
                    Overlap = 400,
                    UseMetadata = true
                })
                .WithTextOptions(new TextHybridParagraphIngestionOptions
                {
                    MinSize = 500,
                    MaxSize = 800,
                    Overlap = 400,
                    UseMetadata = true
                })
                .WithTrainingFiles(existingFiles)
                .Build();

            Console.WriteLine("Adding documents to the vector store...");
            var addedItems = await addPipeline.Train(existingFiles);
            Console.WriteLine($"Added {addedItems.Count} document chunks");

            // ============================================================
            //                  VERIFY FINAL STATE
            // ============================================================

            var finalCount = await pipeline.GetDocumentCount();
            Console.WriteLine($"\nFinal document count: {finalCount}");
            Console.WriteLine($"Total documents added: {finalCount - currentCount}");

            // ============================================================
            //                  TEST SEARCH FUNCTIONALITY
            // ============================================================

            Console.WriteLine("\n=== TESTING SEARCH FUNCTIONALITY ===");
            
            string searchQuery = "Who do I contact regarding HIPPA?";
            Console.WriteLine($"Searching for: {searchQuery}");

            var searchResults = await pipeline.SearchAndReturnTexts(searchQuery, MaxSearchResults);
            Console.WriteLine($"Found {searchResults.Length} relevant chunks");

            if (searchResults.Length > 0)
            {
                Console.WriteLine("\nSample search results:");
                for (int i = 0; i < Math.Min(3, searchResults.Length); i++)
                {
                    Console.WriteLine($"Result {i + 1}: {searchResults[i].Substring(0, Math.Min(100, searchResults[i].Length))}...");
                }
            }

            // ============================================================
            //                  DOCUMENT MANAGEMENT
            // ============================================================

            Console.WriteLine("\n=== DOCUMENT MANAGEMENT ===");
            
            var documentIds = await pipeline.GetDocumentIds();
            Console.WriteLine($"Total document IDs: {documentIds.Count()}");
            
            if (documentIds.Any())
            {
                Console.WriteLine("Sample document IDs:");
                foreach (var id in documentIds.Take(5))
                {
                    Console.WriteLine($"  - {id}");
                }
            }

            Console.WriteLine("\nDemo complete! You can now add more documents to your vector store at any time.");
        }
    }
} 