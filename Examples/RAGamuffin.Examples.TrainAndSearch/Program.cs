using InstructSharp.Clients.ChatGPT;
using InstructSharp.Core;
using RAGamuffin.Core;
using RAGamuffin.Embedding;
using RAGamuffin.Ingestion;
using RAGamuffin.VectorStores;

// ##############################
// SETUP
// ##############################

// File paths for the ONNX embedding model and tokenizer
const string EMBEDDING_MODEL_PATH = @"C:\RAGamuffin\model.onnx"; //https://huggingface.co/sentence-transformers/all-mpnet-base-v2/blob/main/onnx/model.onnx
const string EMBEDDING_TOKENIZER_PATH = @"C:\RAGamuffin\tokenizer.json"; //https://huggingface.co/sentence-transformers/all-mpnet-base-v2/resolve/main/tokenizer.json

// SQLite database configuration
const string DATABASE_LOCATION = @"C:\RAGamuffin\";
const string DATABASE_NAME = @"RAGamuffin_documents.db";
const string DATABASE_COLLECTION_NAME = "handbook_documents";

string DATABASE_FULL_PATH = Path.Combine(DATABASE_LOCATION, DATABASE_NAME);

const string CHATGPT_API_KEY = "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";

// Flag to control whether to rebuild the vector database from source files
const bool RETRAIN_DATA = false;

// Maximum number of results to return from the vector store search
const int MAX_SEARCH_RESULTS = 5;

// Search query to find relevant information in the ingested documents
string searchQuery = "Are there scenarios where I would be exmept of overtime?";

// ##############################
// TRAINING DOCUMENTS
// ##############################

// List of documents to ingest (PDFs, etc.)
string[] TRAINING_FILES = [
    @"C:\RAGamuffin\training-files\company-handbook.pdf"
];

// ##############################
// EMBEDDING
// ##############################

// Initialize the embedder using an ONNX model
IEmbedder embedder = new OnnxEmbedder(EMBEDDING_MODEL_PATH, EMBEDDING_TOKENIZER_PATH);

// Set up the PDF ingestion engine with paragraph-chunking options
IIngestionEngine pdfIngestionEngine = new PdfIngestionEngine();
PdfHybridParagraphIngestionOptions pdfIngestOptions = new()
{
    MaxSize = 1200, // Maximum size of each chunk in characters
    Overlap = 500 // Overlap size in characters (to avoid losing context)
};

// Initialize SQLite-based vector store (auto-retrains if RETRAIN_DATA is true)
IVectorStore vectorStore = new SqliteVectorStoreProvider(DATABASE_FULL_PATH, DATABASE_COLLECTION_NAME, RETRAIN_DATA);

// ##############################
// TRAINING
// ##############################

if (RETRAIN_DATA)
{
    // Ingest documents and compute embeddings
    List<IngestedItem> ingestedItems = new();

    Console.WriteLine("Chunking Data...");
    foreach (string file in TRAINING_FILES)
    {
        var items = await pdfIngestionEngine.IngestAsync(file, pdfIngestOptions);
        ingestedItems.AddRange(items);
    }

    Console.WriteLine("Vectorizing Data...");
    foreach(IngestedItem ingestedItem in ingestedItems)
    {
        // Generate embeddings for each chunk
        ingestedItem.Vectors = await embedder.EmbedAsync(ingestedItem.Text);
        // Upsert into the vector store (insert or update)
        await vectorStore.UpsertAsync(ingestedItem.Id, ingestedItem.Vectors, ingestedItem.Metadata);
    }
}

// ##############################
// VECTOR SEARCH
// ##############################

// Define the user query and compute its embedding
float[] searchQueryVectorized = await embedder.EmbedAsync(searchQuery);
Console.WriteLine($"Searching Vector Store for query: {searchQuery}");

// Perform similarity search in the vector store
var searchResults = await vectorStore.SearchAsync(searchQueryVectorized, MAX_SEARCH_RESULTS);

// ##############################
// PREPARE CONTEXT FOR LLM
// ##############################

List<string> contextTexts = new List<string>();
foreach(var result in searchResults)
{
    // Extract the original text from metadata for LLM context
    if (result.MetaData != null && result.MetaData.ContainsKey("text"))
    {
        string text = result.MetaData["text"].ToString() ?? "";
        contextTexts.Add(text);
    }
}

// Combine top results into a single context string
string contextForLLM = string.Join("\n\n", contextTexts);

// ##############################
// SEND TO LLM
// ##############################

// Prepare ChatGPT client for querying the LLM
ChatGPTClient chatGPTClient = new ChatGPTClient(CHATGPT_API_KEY);

// Instructions to guide the LLM's response
string model_instructions = "You are a helpful assistant. Answer the question based on the provided context. Only refer to the context provided to elimenate false information. Try not to mention context, if you _have_ to mention it, say something like 'according to my training data' or something. !!!Important: try and provide the literal information provided from the context.";

// Input combining user question and retrieved context
string model_input = $"Here is the users question: {searchQuery} \n\n Here is the context found: {contextForLLM}";

Console.WriteLine("=========================== CONTEXT PROVIDED =======================");
Console.WriteLine(model_input);
Console.WriteLine("=========================== END CONTEXT PROVIDED =======================");
Console.WriteLine();

// Create the chat request with model selection and input
ChatGPTRequest chatGPTRequest = new()
{
    Model = ChatGPTModels.GPT4o,
    Instructions = model_instructions,
    Input = model_input
};

Console.WriteLine("Waiting for LLM to respond...");

// Send the request and await the response
LLMResponse<string> answer = await chatGPTClient.QueryAsync<string>(chatGPTRequest);

Console.WriteLine("Answer from LLM:");
Console.WriteLine();
Console.WriteLine(answer.Result);

Console.ReadLine();