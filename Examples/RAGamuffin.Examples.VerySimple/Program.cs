using RAGamuffin.Factories;
using InstructSharp;
using InstructSharp.Clients.ChatGPT;
using InstructSharp.Core;

namespace RAGamuffin.Examples.VerySimple;

/// <summary>
/// RAGamuffin Builder Showcase
/// 
/// Demonstrates the flexible builder pattern and integration with InstructSharp.
/// </summary>
public class Program
{
    private const string BASE_DIRECTORY = @"C:\RAGamuffin\";
    private const string TRAINING_FILES_DIRECTORY = @"C:\RAGamuffin\training-files\";
    private const string CHATGPT_API_KEY = "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";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("RAGamuffin Builder Showcase");
        Console.WriteLine("===========================\n");

        try
        {
            await RunBuilderShowcase();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    private static async Task RunBuilderShowcase()
    {
        // Fluent Builder Pattern
        Console.WriteLine("Creating RAG system with fluent builder...");
        using var rag = RAGBuilder
            .Create()
            .WithEmbedding(
                Path.Combine(BASE_DIRECTORY, "model.onnx"),
                Path.Combine(BASE_DIRECTORY, "tokenizer.json"))
            .WithDatabase(BASE_DIRECTORY, "RAGamuffin_vectors.db", "documents")
            .WithChunking(chunkSize: 1200, overlap: 500)
            .WithMetadata(true)
            .WithDatabaseRecreation(false)
            .WithDefaultSearchResults(5)
            .BuildSystem();

        // Ingest and search
        Console.WriteLine("Ingesting documents...");
        await rag.IngestDirectoryAsync(TRAINING_FILES_DIRECTORY);

        Console.WriteLine("Performing search...");
        var searchQuery = "Does Workwave drug test?";
        
        // Debug: Show the query vector
        var embedder = rag.GetEmbedder();
        var queryVector = await embedder.EmbedAsync(searchQuery);
        Console.WriteLine($"Query: '{searchQuery}'");
        Console.WriteLine($"Query vector dimension: {queryVector.Length}");
        Console.WriteLine($"Query vector first 5 values: [{string.Join(", ", queryVector.Take(5))}]");
        Console.WriteLine($"Query vector sum: {queryVector.Sum()}");
        
        var results = await rag.SearchAsync(searchQuery);
        
        // Debug: Show search results with scores
        Console.WriteLine($"\nSearch results ({results.Count()} items):");
        foreach (var (key, score, metadata) in results)
        {
            Console.WriteLine($"  Score: {score:F4}, Key: {key}");
            if (metadata?.ContainsKey("text") == true)
            {
                var text = metadata["text"].ToString();
                Console.WriteLine($"  Text preview: {(text?.Length > 100 ? text[..100] + "..." : text)}");
            }
        }
        
        var contextForLLM = SimpleRAGSystem.ExtractContextFromResults(results);

        // Test with a different query to see if results change
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("Testing with different query...");
        var differentQuery = "How do I submit expenses?";
        var differentQueryVector = await embedder.EmbedAsync(differentQuery);
        Console.WriteLine($"Different Query: '{differentQuery}'");
        Console.WriteLine($"Different Query vector first 5 values: [{string.Join(", ", differentQueryVector.Take(5))}]");
        Console.WriteLine($"Different Query vector sum: {differentQueryVector.Sum()}");
        
        var differentResults = await rag.SearchAsync(differentQuery);
        Console.WriteLine($"\nDifferent search results ({differentResults.Count()} items):");
        foreach (var (key, score, metadata) in differentResults)
        {
            Console.WriteLine($"  Score: {score:F4}, Key: {key}");
        }

        // Test searching for actual content that should exist
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("Testing search for 'WorkWave' (company name)...");
        var workwaveResults = await rag.SearchAsync("WorkWave");
        Console.WriteLine($"\nWorkWave search results ({workwaveResults.Count()} items):");
        foreach (var (key, score, metadata) in workwaveResults)
        {
            Console.WriteLine($"  Score: {score:F4}, Key: {key}");
            if (metadata?.ContainsKey("text") == true)
            {
                var text = metadata["text"].ToString();
                Console.WriteLine($"  Text preview: {(text?.Length > 100 ? text[..100] + "..." : text)}");
            }
        }

        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("Testing search for 'drug'...");
        var drugResults = await rag.SearchAsync("drug");
        Console.WriteLine($"\nDrug search results ({drugResults.Count()} items):");
        foreach (var (key, score, metadata) in drugResults)
        {
            Console.WriteLine($"  Score: {score:F4}, Key: {key}");
            if (metadata?.ContainsKey("text") == true)
            {
                var text = metadata["text"].ToString();
                Console.WriteLine($"  Text preview: {(text?.Length > 100 ? text[..100] + "..." : text)}");
            }
        }

        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("Testing search for 'violence'...");
        var violenceResults = await rag.SearchAsync("violence");
        Console.WriteLine($"\nViolence search results ({violenceResults.Count()} items):");
        foreach (var (key, score, metadata) in violenceResults)
        {
            Console.WriteLine($"  Score: {score:F4}, Key: {key}");
            if (metadata?.ContainsKey("text") == true)
            {
                var text = metadata["text"].ToString();
                Console.WriteLine($"  Text preview: {(text?.Length > 100 ? text[..100] + "..." : text)}");
            }
        }

        // Test if search results are consistent
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("Testing search consistency - same query twice...");
        var testQuery = "test";
        var results1 = await rag.SearchAsync(testQuery);
        var results2 = await rag.SearchAsync(testQuery);
        
        Console.WriteLine($"\nFirst search for '{testQuery}' ({results1.Count()} items):");
        foreach (var (key, score, metadata) in results1)
        {
            Console.WriteLine($"  Score: {score:F4}, Key: {key}");
        }
        
        Console.WriteLine($"\nSecond search for '{testQuery}' ({results2.Count()} items):");
        foreach (var (key, score, metadata) in results2)
        {
            Console.WriteLine($"  Score: {score:F4}, Key: {key}");
        }

        // LLM Integration
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("Integrating with ChatGPT...");
        await IntegrateWithLLM(searchQuery, contextForLLM);
    }

    private static async Task IntegrateWithLLM(string searchQuery, string contextForLLM)
    {
        try
        {
            ChatGPTClient client = new(CHATGPT_API_KEY);

            ChatGPTRequest request = new()
            {
                Model = ChatGPTModels.GPT4o,
                Instructions = @"You are a helpful HR assistant. Answer the question based on the provided context. 

IMPORTANT GUIDELINES:
1. Only refer to information found in the provided context
2. If the context doesn't contain relevant information to answer the question, say: 'I don't have specific information about [topic] in the available documentation. You should contact HR or your supervisor for guidance on this matter.'
3. If the context contains related but not directly relevant information, acknowledge what information is available and suggest contacting HR for specific guidance
4. Be professional and helpful in your responses
5. Don't make up information that isn't in the context",
                Input = @$"Here is the users question: {searchQuery}

And here is the context I have found:
{contextForLLM}"
            };

            Console.WriteLine("Context Sent to LLM: " + request.Input);

            LLMResponse<string> answer = await client.QueryAsync<string>(request);

            Console.WriteLine("\nQuery: " + searchQuery);
            Console.WriteLine("Answer: " + answer.Result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LLM integration failed: {ex.Message}");
            Console.WriteLine("\nQuery: " + searchQuery);
            Console.WriteLine("Context would be sent to LLM for processing.");
        }
    }
} 