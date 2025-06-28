using RAGamuffin.Builders;
using RAGamuffin.Core;
using RAGamuffin.Embedding;
using RAGamuffin.Enums;
using RAGamuffin.Ingestion;

namespace RAGamuffin.Examples.RealTimeIngestion
{
    /// <summary>
    /// RAGamuffin Real-Time Data Ingestion Example
    /// 
    /// This example demonstrates how to add new data to your vector store in real-time
    /// without retraining the entire model. This is perfect for scenarios like:
    /// - HTTP webhooks receiving new documents
    /// - Streaming data from APIs
    /// - Real-time log processing
    /// - Live document updates
    /// 
    /// Key Concept: Only new data gets processed and added as vectors.
    /// The embedding model never changes - just new vectors are added to the store.
    /// </summary>
    static class Program
    {
        // Configuration
        private const string EmbeddingModelPath = @"C:\RAGamuffin\model.onnx";
        private const string EmbeddingTokenizerPath = @"C:\RAGamuffin\tokenizer.json";
        private const string DatabaseDirectory = @"C:\RAGamuffin\";
        private const string DatabaseName = "RAGamuffin_realtime.db";
        private const string CollectionName = "realtime_data";

        // In-memory data collection
        private static readonly List<RealTimeDataItem> PendingData = new();

        static async Task Main()
        {
            var dbPath = Path.Combine(DatabaseDirectory, DatabaseName);
            Console.WriteLine($"Real-Time Data Ingestion Demo");
            Console.WriteLine($"Database: {dbPath}");
            Console.WriteLine();

            // Initialize the embedding model (this never changes)
            var embedder = new OnnxEmbedder(EmbeddingModelPath, EmbeddingTokenizerPath);
            Console.WriteLine("✓ Embedding model initialized");

            // Build the pipeline for real-time ingestion
            // Note: No training files needed at build time for incremental strategies
            var pipeline = new IngestionTrainingBuilder()
                .WithEmbeddingModel(embedder)
                .WithVectorDatabase(new SqliteDatabaseModel(dbPath, CollectionName))
                .WithTrainingStrategy(TrainingStrategy.IncrementalAdd) // Only add new data, skip existing
                .WithTextOptions(new TextHybridParagraphIngestionOptions
                {
                    MinSize = 100,
                    MaxSize = 500,
                    Overlap = 50,
                    UseMetadata = true
                })
                .Build();

            // Check initial state
            var initialCount = await pipeline.GetDocumentCount();
            Console.WriteLine($"✓ Initial document count: {initialCount}");
            Console.WriteLine();

            Console.WriteLine("=== STARTING REAL-TIME INGESTION ===");
            Console.WriteLine("Simulating data arrival every 10 seconds...");
            Console.WriteLine("Press Ctrl+C to stop");
            Console.WriteLine();

            var dataCounter = 0;
            var cancellationTokenSource = new CancellationTokenSource();

            // Handle graceful shutdown
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
                Console.WriteLine("\nStopping real-time ingestion...");
            };

            try
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Simulate receiving new data (like from an HTTP webhook)
                    var newData = await SimulateDataArrivalAsync();
                    dataCounter++;

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] New data received:");
                    Console.WriteLine($"  Content: {newData.Substring(0, Math.Min(60, newData.Length))}...");

                    // Add to in-memory collection
                    PendingData.Add(new RealTimeDataItem
                    {
                        Id = $"realtime_data_{dataCounter}",
                        Content = newData,
                        Timestamp = DateTime.Now
                    });

                    // Process all pending data using direct text training
                    var addedItems = await ProcessPendingDataAsync(pipeline);
                    
                    if (addedItems.Count > 0)
                    {
                        var currentCount = await pipeline.GetDocumentCount();
                        Console.WriteLine($"  ✓ Added {addedItems.Count} chunks. Total documents: {currentCount}");
                    }
                    else
                    {
                        Console.WriteLine("  - No new data to process");
                    }

                    // Test search functionality periodically
                    if (dataCounter % 3 == 0)
                    {
                        await TestSearchAsync(pipeline, newData);
                    }

                    Console.WriteLine();

                    // Wait for next data arrival
                    await Task.Delay(10000, cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Real-time ingestion stopped by user.");
            }

            // Final summary
            var finalCount = await pipeline.GetDocumentCount();
            Console.WriteLine("\n=== FINAL SUMMARY ===");
            Console.WriteLine($"Total data points processed: {dataCounter}");
            Console.WriteLine($"Documents added: {finalCount - initialCount}");
            Console.WriteLine($"Final document count: {finalCount}");
            Console.WriteLine();
            Console.WriteLine("Key Takeaway: New data was added without retraining the entire model!");
        }

        /// <summary>
        /// Simulates receiving new data (like from an HTTP webhook or API)
        /// In a real application, this would be your actual data source
        /// </summary>
        static async Task<string> SimulateDataArrivalAsync()
        {
            // Simulate network delay
            await Task.Delay(Random.Shared.Next(100, 300));

            var currentTime = DateTime.Now;
            var dataTypes = new[]
            {
                $"System status update at {currentTime:HH:mm:ss}: All services operational. CPU: {Random.Shared.Next(10, 90)}%, Memory: {Random.Shared.Next(20, 80)}%",
                $"User login at {currentTime:HH:mm:ss}: Authentication successful. Session ID: {Guid.NewGuid():N}. IP: 192.168.1.{Random.Shared.Next(1, 255)}",
                $"Database backup completed at {currentTime:HH:mm:ss}: Size {Random.Shared.Next(100, 999)}MB, Compression {Random.Shared.Next(60, 85)}%, Status: Success",
                $"API request at {currentTime:HH:mm:ss}: Endpoint /api/data, Response time {Random.Shared.Next(50, 300)}ms, Status: 200 OK",
                $"System alert at {currentTime:HH:mm:ss}: Disk usage {Random.Shared.Next(70, 95)}%, Threshold: 80%, Action: Monitor"
            };

            return dataTypes[Random.Shared.Next(dataTypes.Length)];
        }

        /// <summary>
        /// Processes all pending data in memory and adds it to the vector store
        /// This is the core of real-time ingestion - no full retraining needed!
        /// </summary>
        static async Task<List<IngestedItem>> ProcessPendingDataAsync(RAGamuffinModel pipeline)
        {
            if (PendingData.Count == 0)
                return new List<IngestedItem>();

            // Convert pending data to TextItems for direct processing
            var textItems = PendingData.Select(item => new TextItem(
                item.Id,
                item.Content,
                item.Timestamp
            )).ToArray();

            // Process all text items directly (no temp files!)
            var addedItems = await pipeline.TrainWithText(textItems);

            // Clear the pending data collection
            PendingData.Clear();

            return addedItems;
        }

        /// <summary>
        /// Tests that new data is immediately searchable
        /// Demonstrates that the vector store is updated in real-time
        /// </summary>
        static async Task TestSearchAsync(RAGamuffinModel pipeline, string newData)
        {
            Console.WriteLine("  - Testing search functionality...");
            
            // Extract a search term from the new data
            var searchTerms = new[] { "system status", "authentication", "backup", "API request", "disk usage" };
            var searchTerm = searchTerms.FirstOrDefault(term => 
                newData.Contains(term, StringComparison.OrdinalIgnoreCase)) ?? "system";
            
            var searchResults = await pipeline.SearchAndReturnTexts(searchTerm, 3);
            Console.WriteLine($"    Search for '{searchTerm}': Found {searchResults.Length} results");
            
            if (searchResults.Length > 0)
            {
                var latestResult = searchResults.First();
                Console.WriteLine($"    Latest: {latestResult.Substring(0, Math.Min(70, latestResult.Length))}...");
            }
        }
    }

    /// <summary>
    /// Represents a piece of real-time data waiting to be processed
    /// </summary>
    public class RealTimeDataItem
    {
        public string Id { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
} 