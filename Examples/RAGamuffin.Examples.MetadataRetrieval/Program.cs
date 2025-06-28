using RAGamuffin;
using RAGamuffin.Builders;
using RAGamuffin.Core;
using RAGamuffin.Enums;
using RAGamuffin.Embedding;
using RAGamuffin.VectorStores;
using System.Text.Json;

namespace RAGamuffin.Examples.MetadataRetrieval;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== RAGamuffin Metadata Retrieval Demo ===\n");

        // Initialize the RAGamuffin builder
        var embedder = new OnnxEmbedder(@"C:\RAGamuffin\model.onnx", @"C:\RAGamuffin\tokenizer.json");
        var vectorDb = new SqliteDatabaseModel(@"C:\RAGamuffin\metadata_demo.db", "metadata_collection");
        var builder = new IngestionTrainingBuilder()
            .WithEmbeddingModel(embedder)
            .WithVectorDatabase(vectorDb)
            .WithTrainingStrategy(TrainingStrategy.RetrainFromScratch);

        // Create sample data with rich metadata
        var textItems = CreateSampleDataWithMetadata();
        
        Console.WriteLine("Training model with sample data containing rich metadata...");
        var (ingestedItems, model) = await builder.TrainWithText(textItems);
        
        Console.WriteLine($"\nTotal documents in store: {await model.GetDocumentCount()}");

        var searchResults = await model.Search("research papers", 5);

        await RunSingleSearch("I'm only looking for programmers that deal with microsoft and lunix based systems", model);
        await RunSingleSearch("data science", model);
        await RunSingleSearch("statistical analysis", model);
        await RunSingleSearch("user research", model);
        await RunSingleSearch("published papers", model);


        //// Demonstrate various metadata retrieval capabilities
        //await DemonstrateMetadataRetrieval(model);

        //Console.WriteLine("\n=== Demo Complete ===");
    }

    static async Task RunSingleSearch(string query, RAGamuffinModel model)
    {
        var searchResults = await model.Search(query, 5);
        Console.WriteLine($"\nSearch Results for {query}:");
        foreach (var result in searchResults)
        {
            Console.WriteLine($"- {result.MetaData["name"]} ({result.MetaData["title"]}) - Score: {result.Score:F3}");
        }
        Console.WriteLine("---");
    }

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

    static async Task DemonstrateMetadataRetrieval(RAGamuffinModel model)
    {
        Console.WriteLine("\n=== Metadata Retrieval Demonstrations ===\n");

        // 1. Get all documents with their metadata
        Console.WriteLine("1. All Documents with Metadata:");
        var allDocs = await model.GetAllDocumentsMetadata();
        foreach (var doc in allDocs)
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
        Console.WriteLine("5. Active Employees:");
        var activeDocs = await model.GetDocumentsByMetadataFilter("isActive", true);
        foreach (var doc in activeDocs)
        {
            var name = GetMetadataValue(doc.Metadata, "name");
            var department = GetMetadataValue(doc.Metadata, "department");
            Console.WriteLine($"  {name} - {department}");
        }
        Console.WriteLine();

        // 6. Get document IDs by location
        Console.WriteLine("6. Document IDs for Remote Workers:");
        var remoteDocIds = await model.GetDocumentIdsByMetadataFilter("location", "Remote");
        foreach (var docId in remoteDocIds)
        {
            Console.WriteLine($"  {docId}");
        }
        Console.WriteLine();

        // 7. Filter by hire date range
        Console.WriteLine("7. Employees Hired in 2021:");
        var hireDate2021Docs = await model.GetDocumentsByMetadataRange("hireDate", "2021-01-01", "2021-12-31");
        foreach (var doc in hireDate2021Docs)
        {
            var name = GetMetadataValue(doc.Metadata, "name");
            var hireDate = GetMetadataValue(doc.Metadata, "hireDate");
            Console.WriteLine($"  {name} - Hired: {hireDate}");
        }
        Console.WriteLine();

        // 8. Demonstrate complex metadata (skills array)
        Console.WriteLine("8. Employees with Python Skills:");
        var pythonDocs = await model.GetDocumentsByMetadataFilter("skills", "Python");
        foreach (var doc in pythonDocs)
        {
            var name = GetMetadataValue(doc.Metadata, "name");
            var skills = GetMetadataValue(doc.Metadata, "skills");
            Console.WriteLine($"  {name} - Skills: {skills}");
        }
        Console.WriteLine();

        // 9. Search and retrieve metadata for search results
        Console.WriteLine("9. Vector Search with Metadata:");
        var searchResults = await model.Search("dotnet programming experience", 3);
        foreach (var result in searchResults)
        {
            var name = GetMetadataValue(result.MetaData, "name");
            var title = GetMetadataValue(result.MetaData, "title");
            var score = result.Score;
            Console.WriteLine($"  {name} ({title}) - Score: {score:F3}");
        }
        Console.WriteLine();
    }

    private static string GetMetadataValue(IDictionary<string, object>? metadata, string key)
    {
        if (metadata?.TryGetValue(key, out var value) == true)
        {
            return value?.ToString() ?? "N/A";
        }
        return "N/A";
    }
} 