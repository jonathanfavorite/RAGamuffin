![RAGamuffin Banner](https://i.imgur.com/i1JOlz9.jpeg)

<p align="center">
  <a href="https://www.nuget.org/packages/RAGamuffin"><img src="https://img.shields.io/nuget/v/RAGamuffin?style=for-the-badge&color=brightgreen" alt="NuGet Version"></a>
  <a href="https://github.com/jonathanfavorite/RAGamuffin/actions"><img src="https://img.shields.io/github/actions/workflow/status/jonathanfavorite/RAGamuffin/build.yml?style=for-the-badge" alt="Build Status"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/jonathanfavorite/RAGamuffin?style=for-the-badge&color=blue" alt="MIT License"></a>
</p>

A lightweight, cross-platform .NET library for building RAG (Retrieval-Augmented Generation) pipelines with local embedding models and SQLite vector storage.

## 🚀 Features

- **Local Embedding Models**: Use ONNX models for offline, privacy-focused embeddings
- **SQLite Vector Storage**: Lightweight, file-based vector database with no external dependencies
- **Multi-Format Support**: Process PDFs and text files with intelligent chunking
- **Flexible Training Strategies**: Retrain from scratch, incremental updates, or add-only modes
- **Real-time Ingestion**: Stream text content directly into your vector store
- **Metadata Preservation**: Maintain document context and metadata throughout the pipeline
- **Cross-Platform**: Works on Windows, macOS, and Linux with .NET 8.0+

## 🎯 Quick Start

### Installation

```bash
dotnet add package RAGamuffin
```

### Basic Usage

```csharp
using RAGamuffin.Builders;
using RAGamuffin.Core;
using RAGamuffin.Embedding;
using RAGamuffin.Enums;

// 1. Set up your embedding model (download from HuggingFace)
var embedder = new OnnxEmbedder("path/to/model.onnx", "path/to/tokenizer.json");

// 2. Configure your vector database
var vectorDb = new SqliteDatabaseModel("documents.db", "my_collection");

// 3. Build and train your pipeline
var pipeline = new IngestionTrainingBuilder()
    .WithEmbeddingModel(embedder)
    .WithVectorDatabase(vectorDb)
    .WithTrainingStrategy(TrainingStrategy.RetrainFromScratch)
    .WithTrainingFiles(new[] { "document.pdf" })
    .Build();

var ingestedItems = await pipeline.Train();

// 4. Search your documents
string[] results = await pipeline.SearchAndReturnTexts("What is the company policy?", 5);
```

### Real-time Text Ingestion

```csharp
// Stream text content directly into your vector store
var textItems = new[]
{
    new TextItem("Meeting notes from Q1", "Q1 was successful with 15% growth..."),
    new TextItem("Product roadmap", "Next quarter we'll launch feature X...")
};

var (ingestedItems, model) = await pipeline.TrainWithText(textItems);
```

### Search Existing Vector Store

```csharp
// Search without retraining
var vectorStore = new SqliteVectorStoreProvider("documents.db", "my_collection");
var searchResults = await vectorStore.SearchAsync("your query", embedder, 5);

// Get metadata
var metadata = await vectorStore.GetAllDocumentsMetadataAsync();
```

## 📚 Examples

Check out the comprehensive examples in the `Examples/` directory:

- **[TrainAndSearch](Examples/RAGamuffin.Examples.TrainAndSearch/)**: Complete RAG pipeline with training and search
- **[SearchExistingVectorStore](Examples/RAGamuffin.Examples.SearchExistingVectorStore/)**: Query existing vector stores with metadata
- **[IncrementalTraining](Examples/RAGamuffin.Examples.IncrementalTraining/)**: Add new documents to existing collections
- **[RealTimeIngestion](Examples/RAGamuffin.Examples.RealTimeIngestion/)**: Stream text content in real-time
- **[MetadataRetrieval](Examples/RAGamuffin.Examples.MetadataRetrieval/)**: Work with document metadata and statistics

## 🔧 Configuration

### Embedding Models

RAGamuffin supports ONNX models for cross-platform compatibility. Recommended starter model:

- **Model**: `all-mpnet-base-v2` from HuggingFace
- **Download**: [Model](https://huggingface.co/sentence-transformers/all-mpnet-base-v2/blob/main/onnx/model.onnx) | [Tokenizer](https://huggingface.co/sentence-transformers/all-mpnet-base-v2/resolve/main/tokenizer.json)

### Training Strategies

- **RetrainFromScratch**: Drop all existing data and retrain
- **IncrementalAdd**: Add new documents (skip if exists)
- **IncrementalUpdate**: Add new documents and update existing ones
- **ProcessOnly**: Only process documents, no vector operations

### Chunking Options

```csharp
// PDF processing options
.WithPdfOptions(new PdfHybridParagraphIngestionOptions
{
    MinSize = 0,        // Minimum chunk size
    MaxSize = 800,      // Maximum chunk size
    Overlap = 400,      // Overlap between chunks
    UseMetadata = true  // Include document metadata
})

// Text processing options
.WithTextOptions(new TextHybridParagraphIngestionOptions
{
    MinSize = 500,      // Minimum chunk size
    MaxSize = 800,      // Maximum chunk size
    Overlap = 400,      // Overlap between chunks
    UseMetadata = true  // Include document metadata
})
```

## 🏗️ Architecture

RAGamuffin is built with a modular architecture:

- **Abstractions**: Clean interfaces for embedding, ingestion, and vector storage
- **Core**: Main pipeline logic and data models
- **Embedding**: ONNX-based embedding providers
- **Ingestion**: PDF and text processing engines
- **VectorStores**: SQLite vector database implementation
- **Builders**: Fluent API for pipeline configuration

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## 🔗 Related Projects

- **[InstructSharp](https://github.com/jonathanfavorite/InstructSharp)**: LLM client library for .NET
- **[PdfPig](https://github.com/UglyToad/PdfPig)**: PDF processing library
- **[Microsoft.ML.OnnxRuntime](https://github.com/microsoft/onnxruntime)**: ONNX model inference

---

**RAGamuffin** - Making RAG pipelines simple and accessible for .NET developers.