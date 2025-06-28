using RAGamuffin;
using RAGamuffin.Builders;
using RAGamuffin.Core;
using RAGamuffin.Enums;
using RAGamuffin.Embedding;
using RAGamuffin.VectorStores;
using System.Text.Json;
using RAGamuffin.Ingestion;

namespace RAGamuffin.Examples.MetadataRetrieval;

/// <summary>
/// RAGamuffin Metadata Retrieval Example
/// 
/// This example demonstrates advanced metadata capabilities including:
/// - Storing rich structured metadata with documents
/// - Filtering search results by metadata fields
/// - Range-based metadata queries
/// - Complex metadata retrieval scenarios
/// 
/// Key Concept: Metadata allows you to store additional context with each document
/// and filter/query based on that context, not just semantic similarity.
/// </summary>
class Program
{
    // ==================== CONFIGURATION ====================
    private const string EmbeddingModelPath = @"C:\RAGamuffin\model.onnx";
    private const string EmbeddingTokenizerPath = @"C:\RAGamuffin\tokenizer.json";
    private const string DatabasePath = @"C:\RAGamuffin\metadata_demo.db";
    private const string CollectionName = "metadata_collection";

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== RAGamuffin Metadata Retrieval Demo ===\n");

        try
        {
            // ============================================================
            //                    SETUP & INITIALIZATION
            // ============================================================

            // Validate model files exist
            if (!File.Exists(EmbeddingModelPath))
            {
                Console.WriteLine($"Error: Embedding model not found at {EmbeddingModelPath}");
                Console.WriteLine("Please download the model from: https://huggingface.co/sentence-transformers/all-mpnet-base-v2");
                return;
            }

            if (!File.Exists(EmbeddingTokenizerPath))
            {
                Console.WriteLine($"Error: Tokenizer not found at {EmbeddingTokenizerPath}");
                Console.WriteLine("Please download the tokenizer from: https://huggingface.co/sentence-transformers/all-mpnet-base-v2");
                return;
            }

            // Initialize components
            var embedder = new OnnxEmbedder(EmbeddingModelPath, EmbeddingTokenizerPath);
            var vectorDb = new SqliteDatabaseModel(DatabasePath, CollectionName);
            
            Console.WriteLine("✓ Embedding model initialized");
            Console.WriteLine("✓ Vector database configured");

            // ============================================================
            //                  BUILD & TRAIN PIPELINE
            // ============================================================

            var builder = new IngestionTrainingBuilder()
                .WithEmbeddingModel(embedder)
                .WithVectorDatabase(vectorDb)
                .WithTrainingStrategy(TrainingStrategy.RetrainFromScratch)
                .WithTextOptions(new TextHybridParagraphIngestionOptions
                {
                    MinSize = 100,
                    MaxSize = 500,
                    Overlap = 50,
                    UseMetadata = true
                });

            // Create sample data with rich metadata
            var textItems = CreateSampleDataWithMetadata();
            
            Console.WriteLine("\nTraining model with sample data containing rich metadata...");
            var (ingestedItems, model) = await builder.TrainWithText(textItems);
            
            Console.WriteLine($"✓ Successfully ingested {ingestedItems.Count} document chunks");
            Console.WriteLine($"✓ Total documents in store: {await model.GetDocumentCount()}");

            // ============================================================
            //                  METADATA RETRIEVAL DEMOS
            // ============================================================

            await DemonstrateMetadataRetrieval(model);

            // ============================================================
            //                  SEARCH WITH METADATA FILTERING
            // ============================================================

            await DemonstrateSearchWithMetadataFiltering(model);

            Console.WriteLine("\n=== Demo Complete ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError during execution: {ex.Message}");
            Console.WriteLine("Please check your configuration and try again.");
        }
    }

    /// <summary>
    /// Demonstrates various metadata retrieval capabilities
    /// </summary>
    static async Task DemonstrateMetadataRetrieval(RAGamuffinModel model)
    {
        Console.WriteLine("\n=== METADATA RETRIEVAL DEMONSTRATIONS ===\n");

        // 1. Get all documents with their metadata
        Console.WriteLine("1. All Documents with Metadata:");
        var allDocs = await model.GetAllDocumentsMetadata();
        foreach (var doc in allDocs.Take(3)) // Show first 3 for brevity
        {
            Console.WriteLine($"  Document: {doc.DocumentId}");
            if (doc.Metadata != null)
            {
                foreach (var kvp in doc.Metadata)
                {
                    Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
                }
            }
            Console.WriteLine();
        }
        Console.WriteLine($"  ... and {allDocs.Count() - 3} more documents\n");

        // 2. Get specific document metadata
        Console.WriteLine("2. Specific Document Metadata:");
        var specificDoc = await model.GetDocumentMetadata("employee_001_chunk_0");
        if (specificDoc != null)
        {
            Console.WriteLine($"  Employee ID: {GetMetadataValue(specificDoc, "employeeId")}");
            Console.WriteLine($"  Name: {GetMetadataValue(specificDoc, "name")}");
            Console.WriteLine($"  Title: {GetMetadataValue(specificDoc, "title")}");
            Console.WriteLine($"  Department: {GetMetadataValue(specificDoc, "department")}");
            Console.WriteLine($"  Salary: {GetMetadataValue(specificDoc, "salary")}");
            Console.WriteLine($"  Location: {GetMetadataValue(specificDoc, "location")}");
            Console.WriteLine($"  Active: {GetMetadataValue(specificDoc, "isActive")}");
        }
        Console.WriteLine();

        // 3. Filter by department
        Console.WriteLine("3. Employees in Engineering Department:");
        var engineeringDocs = await model.GetDocumentsByMetadataFilter("department", "Engineering");
        foreach (var doc in engineeringDocs)
        {
            var name = GetMetadataValue(doc.Metadata, "name");
            var title = GetMetadataValue(doc.Metadata, "title");
            Console.WriteLine($"  {name} - {title}");
        }
        Console.WriteLine();

        // 4. Filter by salary range
        Console.WriteLine("4. Employees with Salary >= 90000:");
        var highSalaryDocs = await model.GetDocumentsByMetadataRange("salary", 90000, int.MaxValue);
        foreach (var doc in highSalaryDocs)
        {
            var name = GetMetadataValue(doc.Metadata, "name");
            var salary = GetMetadataValue(doc.Metadata, "salary");
            Console.WriteLine($"  {name} - ${salary}");
        }
        Console.WriteLine();

        // 5. Filter by active status
        Console.WriteLine("5. Active Employees Only:");
        var activeDocs = await model.GetDocumentsByMetadataFilter("isActive", true);
        foreach (var doc in activeDocs)
        {
            var name = GetMetadataValue(doc.Metadata, "name");
            var department = GetMetadataValue(doc.Metadata, "department");
            Console.WriteLine($"  {name} - {department}");
        }
        Console.WriteLine();

        // 6. Filter by location
        Console.WriteLine("6. Remote Workers:");
        var remoteDocs = await model.GetDocumentsByMetadataFilter("location", "Remote");
        foreach (var doc in remoteDocs)
        {
            var name = GetMetadataValue(doc.Metadata, "name");
            var title = GetMetadataValue(doc.Metadata, "title");
            Console.WriteLine($"  {name} - {title}");
        }
        Console.WriteLine();

        // 7. Date range filtering (hire dates)
        Console.WriteLine("7. Employees hired in 2021:");
        var startDate = new DateTime(2021, 1, 1);
        var endDate = new DateTime(2021, 12, 31);
        var dateRangeDocs = await model.GetDocumentsByMetadataRange("hireDate", startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
        foreach (var doc in dateRangeDocs)
        {
            var name = GetMetadataValue(doc.Metadata, "name");
            var hireDate = GetMetadataValue(doc.Metadata, "hireDate");
            Console.WriteLine($"  {name} - Hired: {hireDate}");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates combining semantic search with metadata filtering
    /// </summary>
    static async Task DemonstrateSearchWithMetadataFiltering(RAGamuffinModel model)
    {
        Console.WriteLine("=== SEARCH WITH METADATA FILTERING ===\n");

        // 1. Basic semantic search
        Console.WriteLine("1. Basic Semantic Search for 'programming':");
        var basicResults = await model.Search("programming", 5);
        foreach (var result in basicResults)
        {
            var name = GetMetadataValue(result.MetaData, "name");
            var title = GetMetadataValue(result.MetaData, "title");
            Console.WriteLine($"  {name} ({title}) - Score: {result.Score:F3}");
        }
        Console.WriteLine();

        // 2. Search with department filter
        Console.WriteLine("2. Search for 'development' in Engineering department:");
        var engineeringResults = await model.Search("development", 5);
        var engineeringFiltered = engineeringResults.Where(r => 
            GetMetadataValue(r.MetaData, "department") == "Engineering");
        
        foreach (var result in engineeringFiltered)
        {
            var name = GetMetadataValue(result.MetaData, "name");
            var title = GetMetadataValue(result.MetaData, "title");
            Console.WriteLine($"  {name} ({title}) - Score: {result.Score:F3}");
        }
        Console.WriteLine();

        // 3. Search with salary filter
        Console.WriteLine("3. Search for 'leadership' among high earners (>= 90000):");
        var leadershipResults = await model.Search("leadership", 5);
        var highEarnerFiltered = leadershipResults.Where(r => 
        {
            var salaryStr = GetMetadataValue(r.MetaData, "salary");
            return int.TryParse(salaryStr, out var salary) && salary >= 90000;
        });
        
        foreach (var result in highEarnerFiltered)
        {
            var name = GetMetadataValue(result.MetaData, "name");
            var salary = GetMetadataValue(result.MetaData, "salary");
            Console.WriteLine($"  {name} (${salary}) - Score: {result.Score:F3}");
        }
        Console.WriteLine();

        // 4. Search with multiple filters
        Console.WriteLine("4. Search for 'research' among active employees in Analytics:");
        var researchResults = await model.Search("research", 5);
        var multiFiltered = researchResults.Where(r => 
            GetMetadataValue(r.MetaData, "isActive") == "True" &&
            GetMetadataValue(r.MetaData, "department") == "Analytics");
        
        foreach (var result in multiFiltered)
        {
            var name = GetMetadataValue(result.MetaData, "name");
            var department = GetMetadataValue(result.MetaData, "department");
            Console.WriteLine($"  {name} ({department}) - Score: {result.Score:F3}");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Creates sample employee data with rich metadata for demonstration
    /// </summary>
    static TextItem[] CreateSampleDataWithMetadata()
    {
        return new TextItem[]
        {
            new TextItem("employee_001", "John Smith is a senior software engineer with 8 years of experience in C# and .NET development. He specializes in web applications and has led several successful projects.")
            {
                Metadata = new Dictionary<string, object>
                {
                    ["employeeId"] = "EMP001",
                    ["name"] = "John Smith",
                    ["title"] = "Senior Software Engineer",
                    ["department"] = "Engineering",
                    ["salary"] = 95000,
                    ["hireDate"] = "2020-03-15",
                    ["skills"] = new[] { "C#", ".NET", "Web Development", "Leadership" },
                    ["location"] = "New York",
                    ["isActive"] = true
                }
            },
            
            new TextItem("employee_002", "Sarah Johnson works as a data scientist in the analytics team. She has expertise in Python, machine learning, and statistical analysis. Sarah has published several research papers.")
            {
                Metadata = new Dictionary<string, object>
                {
                    ["employeeId"] = "EMP002",
                    ["name"] = "Sarah Johnson",
                    ["title"] = "Data Scientist",
                    ["department"] = "Analytics",
                    ["salary"] = 88000,
                    ["hireDate"] = "2021-07-22",
                    ["skills"] = new[] { "Python", "Machine Learning", "Statistics", "Research" },
                    ["location"] = "San Francisco",
                    ["isActive"] = true
                }
            },
            
            new TextItem("employee_003", "Mike Chen is a product manager with 5 years of experience. He has successfully launched 3 major products and has strong skills in agile methodologies and user experience design.")
            {
                Metadata = new Dictionary<string, object>
                {
                    ["employeeId"] = "EMP003",
                    ["name"] = "Mike Chen",
                    ["title"] = "Product Manager",
                    ["department"] = "Product",
                    ["salary"] = 92000,
                    ["hireDate"] = "2019-11-08",
                    ["skills"] = new[] { "Product Management", "Agile", "UX Design", "Leadership" },
                    ["location"] = "Seattle",
                    ["isActive"] = true
                }
            },
            
            new TextItem("employee_004", "Lisa Rodriguez is a UX designer who creates beautiful and intuitive user interfaces. She has worked on mobile apps and web platforms, with a focus on accessibility and user research.")
            {
                Metadata = new Dictionary<string, object>
                {
                    ["employeeId"] = "EMP004",
                    ["name"] = "Lisa Rodriguez",
                    ["title"] = "UX Designer",
                    ["department"] = "Design",
                    ["salary"] = 78000,
                    ["hireDate"] = "2022-01-15",
                    ["skills"] = new[] { "UX Design", "UI Design", "Accessibility", "User Research" },
                    ["location"] = "Austin",
                    ["isActive"] = false
                }
            },
            
            new TextItem("employee_005", "David Wilson is a DevOps engineer responsible for infrastructure automation and cloud deployment. He has expertise in AWS, Docker, and Kubernetes.")
            {
                Metadata = new Dictionary<string, object>
                {
                    ["employeeId"] = "EMP005",
                    ["name"] = "David Wilson",
                    ["title"] = "DevOps Engineer",
                    ["department"] = "Engineering",
                    ["salary"] = 85000,
                    ["hireDate"] = "2021-09-30",
                    ["skills"] = new[] { "AWS", "Docker", "Kubernetes", "Infrastructure" },
                    ["location"] = "Remote",
                    ["isActive"] = true
                }
            }
        };
    }

    /// <summary>
    /// Safely extracts metadata values with null checking
    /// </summary>
    private static string GetMetadataValue(IDictionary<string, object>? metadata, string key)
    {
        if (metadata?.TryGetValue(key, out var value) == true)
        {
            return value?.ToString() ?? "N/A";
        }
        return "N/A";
    }
} 