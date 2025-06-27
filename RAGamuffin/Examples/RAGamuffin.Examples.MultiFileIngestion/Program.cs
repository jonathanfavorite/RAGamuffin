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

        var builder = new IngestionTrainingBuilder(EmbeddingProviders.Onnx)
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
            .DropDatabaseAndRetrain(true);

        try
        {
            var ingestedItems = await builder.BuildAndIngestAsync();
            Console.WriteLine($"Successfully ingested {ingestedItems.Count} items");
            
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