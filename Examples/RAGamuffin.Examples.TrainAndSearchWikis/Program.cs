using InstructSharp.Clients.ChatGPT;
using InstructSharp.Core;
using RAGamuffin.Examples.TrainAndSearchWikis;

RAGManager manager = new(@"C:\RAGamuffin\winteam-wiki.db", @"C:\RAGamuffin\model.onnx", @"C:\RAGamuffin\tokenizer.json");

bool trainFiles = false; // Set to false to skip training and just load the model for searching

if (trainFiles)
{
    await manager.TrainFiles(@"C:\RAGamuffin\training-files\winteam-wiki-repo", new string[] { ".md", ".MD" });
}

Console.WriteLine();
Console.WriteLine();

bool stillAskingQuestion = true;

ChatGPTClient llmClient = new("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");

while (stillAskingQuestion)
{
    Console.WriteLine("Search wiki:");
    string query = Console.ReadLine();
    string context = await manager.Search(query, 5);

   
    ChatGPTRequest request = new()
    {
        Model = ChatGPTModels.GPT4oMini,
        Instructions = @"You are a helpful assistant. Answer the question based on the provided context (from the companies WIKI documentation on internal services).
                Guidelines:
                • Only use information from the provided context to eliminate false information
                • Be direct and factual in your responses
                • If referencing the context, use natural language like 'according to my training data'
                • Provide literal information from the context when possible
                • If the context doesn't contain relevant information, clearly state that",
        Input = $@"Query: {query}

Context Found: {context}"
    };

    
    Console.WriteLine("Searched: " + query);
    Console.WriteLine("Answer:");
    //<string> response = llmClient.StreamQueryAsync<string>(request)  
    await foreach(var response in llmClient.StreamQueryAsync<string>(request))
    {
        Console.Write(response);
    }
    Console.WriteLine();
}

Console.ReadLine();