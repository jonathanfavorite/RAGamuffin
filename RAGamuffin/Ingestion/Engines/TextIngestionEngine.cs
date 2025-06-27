using RAGamuffin.Abstractions;
using RAGamuffin.Common;
using RAGamuffin.Configuration;
using RAGamuffin.Ingestion.Strategies;
using RAGamuffin.Models;
using System.Text;
using UglyToad.PdfPig;

namespace RAGamuffin.Ingestion.Engines;

/// <summary>
/// Engine for ingesting plain text documents and converting them into searchable text chunks.
/// Supports fixed-size chunking with configurable overlap for optimal text processing.
/// </summary>
public class TextIngestionEngine : IIngestionEngine
{
    /// <summary>
    /// Ingests a single text document and returns a list of text chunks.
    /// </summary>
    /// <param name="source">Path to the text file</param>
    /// <param name="options">Configuration options for the ingestion process</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>List of ingested text chunks with metadata</returns>
    /// <exception cref="NotSupportedException">Thrown when an unsupported ingestion strategy is specified</exception>
    public async Task<List<IngestedItem>> IngestAsync(string source, IIngestionOptions options, CancellationToken cancellationToken = default)
    {
        // Read all text from the file
        string fileContent = await ReadTextFileAsync(source, cancellationToken);

        // Use default options if none provided
        if (options is null)
        {
            options = new TextHybridParagraphIngestionOptions();
        }

        // Route to appropriate chunking strategy based on options
        return options.Strategy switch
        {
            IngestionStrategy.HybridParagraphWithThreshold => 
                await ProcessWithHybridParagraphStrategyAsync(fileContent, source, (TextHybridParagraphIngestionOptions)options),
            _ => throw new NotSupportedException($"Ingestion strategy '{options.Strategy}' is not supported for text ingestion.")
        };
    }

    /// <summary>
    /// Ingests multiple text documents and returns combined text chunks.
    /// </summary>
    /// <param name="sources">Array of text file paths</param>
    /// <param name="options">Configuration options for the ingestion process</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>Combined list of ingested text chunks from all documents</returns>
    public async Task<List<IngestedItem>> IngestAsync(string[] sources, IIngestionOptions options, CancellationToken cancellationToken = default)
    {
        var allItems = new List<IngestedItem>();
        
        foreach (var source in sources)
        {
            var items = await IngestAsync(source, options, cancellationToken);
            allItems.AddRange(items);
        }
        
        return allItems;
    }

    /// <summary>
    /// Reads the complete content of a text file asynchronously.
    /// </summary>
    /// <param name="source">Path to the text file</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>Complete text content from the file</returns>
    private async Task<string> ReadTextFileAsync(string source, CancellationToken cancellationToken = default)
    {
        return await File.ReadAllTextAsync(source, cancellationToken);
    }

    /// <summary>
    /// Processes text content using the hybrid paragraph strategy.
    /// This strategy uses fixed-size chunking with overlap to ensure context continuity.
    /// </summary>
    /// <param name="text">Raw text content from file</param>
    /// <param name="source">Original text file path</param>
    /// <param name="options">Chunking configuration options</param>
    /// <returns>List of processed text chunks</returns>
    private async Task<List<IngestedItem>> ProcessWithHybridParagraphStrategyAsync(string text, string source, TextHybridParagraphIngestionOptions options)
    {
        // Return empty list if no text content
        if (string.IsNullOrWhiteSpace(text))
        {
            return await Task.FromResult(new List<IngestedItem>());
        }

        // Step 1: Split text into fixed-size chunks with overlap
        var textChunks = ChunkingHelper.ChunkTextFixedSize(text, options.MaxSize, options.Overlap);

        // Step 2: Convert chunks to IngestedItem objects
        var ingestedItems = textChunks.Select(chunk => CreateIngestedItem(chunk, source, options)).ToList();

        return ingestedItems;
    }

    /// <summary>
    /// Creates an IngestedItem from a text chunk with appropriate metadata.
    /// </summary>
    /// <param name="chunk">Text chunk to process</param>
    /// <param name="source">Original text file path</param>
    /// <param name="options">Configuration options</param>
    /// <returns>IngestedItem with text, metadata, and unique ID</returns>
    private IngestedItem CreateIngestedItem(string chunk, string source, TextHybridParagraphIngestionOptions options)
    {
        var item = new IngestedItem
        {
            Id = HashingHelper.ComputeSha256Hash(chunk),
            Text = chunk,
            Source = source
        };

        // Add metadata if enabled
        if (options.UseMetadata)
        {
            item.Metadata = new Dictionary<string, object>
            {
                ["text"] = chunk,
                ["Length"] = chunk.Length,
                ["source"] = source,
                ["ChunkType"] = "Text_FixedSize",
                ["ProcessingDate"] = DateTime.UtcNow
            };
        }

        return item;
    }
}