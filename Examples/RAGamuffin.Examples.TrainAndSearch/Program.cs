using InstructSharp.Clients.ChatGPT;
using InstructSharp.Core;
using Microsoft.Extensions.VectorData;
using RAGamuffin.Abstractions;
using RAGamuffin.Builders;
using RAGamuffin.Core;
using RAGamuffin.Embedding;
using RAGamuffin.Ingestion;
using RAGamuffin.VectorStores;

// ##############################
// SETUP
// ##############################

// File paths for the ONNX embedding model and tokenizer
//https://huggingface.co/sentence-transformers/all-mpnet-base-v2/blob/main/onnx/model.onnx
const string EMBEDDING_MODEL_PATH = @"C:\RAGamuffin\model.onnx"; 

//https://huggingface.co/sentence-transformers/all-mpnet-base-v2/resolve/main/tokenizer.json
const string EMBEDDING_TOKENIZER_PATH = @"C:\RAGamuffin\tokenizer.json"; 

// SQLite database configuration
const string DATABASE_LOCATION = @"C:\RAGamuffin\";
const string DATABASE_NAME = @"RAGamuffin_documents.db";
const string DATABASE_COLLECTION_NAME = "handbook_documents";

string DATABASE_FULL_PATH = Path.Combine(DATABASE_LOCATION, DATABASE_NAME);

// Flag to control whether to rebuild the vector database from source files
const bool RETRAIN_DATA = true;

const string CHATGPT_API_KEY = "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";

// Maximum number of results to return from the vector store search
const int MAX_SEARCH_RESULTS = 5;

// Search query to find relevant information in the ingested documents
string searchQuery = "Are there scenarios where I would be exmept of overtime?";

// ##############################
// TRAINING DOCUMENTS
// ##############################

string[] TRAINING_FILES = [
    @"C:\RAGamuffin\training-files\company-handbook.pdf"
];

// ##############################
// EMBEDDING
// ##############################

var embedder = new OnnxEmbedder(EMBEDDING_MODEL_PATH, EMBEDDING_TOKENIZER_PATH);
var dbModel = new SqliteDatabaseModel(DATABASE_FULL_PATH, DATABASE_COLLECTION_NAME, RETRAIN_DATA);

var builder = new IngestionTrainingBuilder()
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
    .WithTrainingFiles(TRAINING_FILES)
    .DropDatabaseAndRetrain(true);

Console.WriteLine("Chunking data...");
var ingestedItems = await builder.BuildAndIngestAsync();

Console.WriteLine("Vectorizing & Saving Data...");
foreach (IngestedItem ingestedItem in ingestedItems)
{
    // Generate embeddings for each chunk
    ingestedItem.Vectors = await embedder.EmbedAsync(ingestedItem.Text);
    // Upsert into the vector store (insert or update)
    await vectorStore.UpsertAsync(ingestedItem.Id, ingestedItem.Vectors, ingestedItem.Metadata);
}