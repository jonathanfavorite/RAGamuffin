using RAGamuffin.Builders;
using RAGamuffin.Core;
using RAGamuffin.Embedding;
using RAGamuffin.Enums;

namespace RAGamuffin.Examples.IncrementalTraining
{
    /// <summary>
    /// Examples demonstrating when WithTrainingFiles is required vs optional
    /// </summary>
    public static class UsageExamples
    {
        public static void DemonstrateUsagePatterns()
        {
            var embedder = new OnnxEmbedder("model.onnx", "tokenizer.json");
            var dbModel = new SqliteDatabaseModel("database.db", "collection");

            // ============================================================
            //                  PATTERN 1: STATE MANAGEMENT ONLY
            // ============================================================
            // WithTrainingFiles is NOT required - no training files needed
            var stateManagementPipeline = IngestionTrainingBuilder.CreateForStateManagement(embedder, dbModel).Build();
            
            // You can:
            // - Check document count
            // - Get document IDs
            // - Perform searches
            // - Delete documents
            // But you CANNOT train (no files provided)

            // ============================================================
            //                  PATTERN 2: TRAINING WITH FILES
            // ============================================================
            // WithTrainingFiles IS required - you're actually training
            var trainingFiles = new[] { "document1.pdf", "document2.txt" };
            
            var trainingPipeline = new IngestionTrainingBuilder()
                .WithEmbeddingModel(embedder)
                .WithVectorDatabase(dbModel)
                .WithTrainingStrategy(TrainingStrategy.IncrementalAdd)
                .WithTrainingFiles(trainingFiles) // REQUIRED for training
                .Build();

            // ============================================================
            //                  PATTERN 3: PROCESS ONLY WITH FILES
            // ============================================================
            // WithTrainingFiles is optional - only needed if you call training methods
            var processOnlyPipeline = new IngestionTrainingBuilder()
                .WithEmbeddingModel(embedder)
                .WithVectorDatabase(dbModel)
                .WithTrainingStrategy(TrainingStrategy.ProcessOnly)
                // .WithTrainingFiles(trainingFiles) // Optional - only needed if you call Train()
                .Build();

            // You can still perform all state management operations
            // But if you call Train(), it will return empty list since no files provided
        }
    }
} 