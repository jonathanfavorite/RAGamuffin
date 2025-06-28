using System;
using System.IO;
using System.Threading.Tasks;
using InstructSharp.Core;
using RAGamuffin.Builders;
using RAGamuffin.Core;
using RAGamuffin.Embedding;
using RAGamuffin.Ingestion;
using RAGamuffin.VectorStores;

namespace Example
{
    static class Program
    {
        // Embedding Model
        // Here are the type/model/tokenizers I recommend when getting started
        // - Type: Onnx
        // - Model: https://huggingface.co/sentence-transformers/all-mpnet-base-v2/blob/main/onnx/model.onnx
        // - Tokenizer: https://huggingface.co/sentence-transformers/all-mpnet-base-v2/resolve/main/tokenizer.json
        private const string EmbeddingModelPath = @"C:\RAGamuffin\model.onnx";
        private const string EmbeddingTokenizerPath = @"C:\RAGamuffin\tokenizer.json";

        // Database configuration
        // If the database does not exist, it will be created automatically.
        // To make things simple use the sqlite database provider (created by default).
        private const string DatabaseDirectory = @"C:\RAGamuffin\";
        private const string DatabaseName = "RAGamuffin_documents.db";
        private const string CollectionName = "handbook_documents";

        // Set to true if you want to drop/clear the database and retrain the data.
        private const bool RetrainData = true;

        // Maximum number of search results to return
        private const int MaxSearchResults = 5;

        static async Task Main()
        {
            var dbPath = Path.Combine(DatabaseDirectory, DatabaseName);

            var trainingFiles = new[] 
            {
                @"C:\RAGamuffin\training-files\company-handbook.pdf"
            };

            // Build ingestion pipeline
            var pipeline = new IngestionTrainingBuilder()
                .WithEmbeddingModel(new OnnxEmbedder(EmbeddingModelPath, EmbeddingTokenizerPath))
                .WithVectorDatabase(new SqliteDatabaseModel(dbPath, CollectionName, RetrainData))
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
                .WithTrainingFiles(trainingFiles)
                .DropDatabaseAndRetrain(RetrainData)
                .Build();

            // Train with specified files
            var ingestedItems = await pipeline.Train(trainingFiles);

            // Execute a simple vector search
            var results = await pipeline.Search("What are the company policies?", MaxSearchResults);

        }
    }
}
