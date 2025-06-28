using RAGamuffin.Abstractions;
using RAGamuffin.Builders;
using RAGamuffin.Core;
using RAGamuffin.Embedding;
using RAGamuffin.Enums;
using RAGamuffin.Ingestion;

namespace RAGamuffin.Examples.MultiFileIngestion;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("RAGamuffin Multi-File Ingestion Example");
        Console.WriteLine("======================================");

        var embedder = new OnnxEmbedder("path/to/model.onnx", "path/to/tokenizer.json");
        var dbModel = new SqliteDatabaseModel("test.db", "test_collection", true);

        // Build the model and get access to all components
        var ragModel = new IngestionTrainingBuilder()
            .WithEmbeddingModel(embedder)
            .WithVectorDatabase(dbModel)
            .WithPdfOptions(new PdfHybridParagraphIngestionOptions
            {
                MinSize = 0,
                MaxSize = 1200,
                Overlap = 500,
                UseMetadata = true
            })
            .WithTextOptions(new TextHybridParagraphIngestionOptions
            {
                MinSize = 500,
                MaxSize = 1000,
                Overlap = 200,
                UseMetadata = true
            })
            .WithTrainingFiles(new string[]
            {
                "path/to/document1.pdf",
                "path/to/document2.txt",
                "path/to/document3.md",
                "path/to/document4.html"
            })
            .DropDatabaseAndRetrain(true)
            .Build();

        try
        {
            // Train the model
            var ingestedItems = await ragModel.Train(new string[]
            {
                "path/to/document1.pdf",
                "path/to/document2.txt",
                "path/to/document3.md",
                "path/to/document4.html"
            });
            
            Console.WriteLine($"Training complete! Processed {ingestedItems.Count} items");
            
            // Search using the same vector store instance
            var searchQuery = "What are the company policies?";
            var results = await ragModel.Search(searchQuery, 5);
            
            Console.WriteLine($"\nSearch results for: '{searchQuery}'");
            foreach (var result in results)
            {
                Console.WriteLine($"Score: {result.Score:P2} — ID: {result.Key}");
                if (result.MetaData != null && result.MetaData.ContainsKey("source"))
                {
                    Console.WriteLine($"  Source: {result.MetaData["source"]}");
                }
            }
            
            // Or access components directly
            var directResults = await ragModel.VectorStore.SearchAsync("Another query", ragModel.Embedder, 3);
            
            foreach (var item in ingestedItems.Take(3))
            {
                Console.WriteLine($"Item ID: {item.Id}");
                Console.WriteLine($"Source: {item.Source}");
                Console.WriteLine($"Text Length: {item.Text.Length}");
                Console.WriteLine("---");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during ingestion: {ex.Message}");
        }
    }
} 