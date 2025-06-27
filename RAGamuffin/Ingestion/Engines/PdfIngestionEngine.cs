using RAGamuffin.Abstractions;
using RAGamuffin.Common;
using RAGamuffin.Configuration;
using RAGamuffin.Ingestion.Strategies;
using RAGamuffin.Models;
using System.Text;
using UglyToad.PdfPig;

namespace RAGamuffin.Ingestion.Engines;

/// <summary>
/// Engine for ingesting PDF documents and converting them into searchable text chunks.
/// Supports intelligent paragraph-based chunking with configurable size thresholds and overlap.
/// </summary>
public class PdfIngestionEngine : IIngestionEngine
{
    /// <summary>
    /// Ingests a single PDF document and returns a list of text chunks.
    /// </summary>
    /// <param name="source">Path to the PDF file</param>
    /// <param name="options">Configuration options for the ingestion process</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>List of ingested text chunks with metadata</returns>
    /// <exception cref="NotSupportedException">Thrown when an unsupported ingestion strategy is specified</exception>
    public async Task<List<IngestedItem>> IngestAsync(string source, IIngestionOptions options, CancellationToken cancellationToken = default)
    {
        // Extract all text from the PDF document
        string extractedText = await ExtractTextFromPdfAsync(source, cancellationToken);

        // Use default options if none provided
        if (options is null)
        {
            options = new PdfHybridParagraphIngestionOptions();
        }

        // Route to appropriate chunking strategy based on options
        return options.Strategy switch
        {
            IngestionStrategy.HybridParagraphWithThreshold => 
                await ProcessWithHybridParagraphStrategyAsync(extractedText, source, (PdfHybridParagraphIngestionOptions)options),
            _ => throw new NotSupportedException($"Ingestion strategy '{options.Strategy}' is not supported for PDF ingestion.")
        };
    }

    /// <summary>
    /// Ingests multiple PDF documents and returns combined text chunks.
    /// </summary>
    /// <param name="sources">Array of PDF file paths</param>
    /// <param name="options">Configuration options for the ingestion process</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>Combined list of ingested text chunks from all documents</returns>
    public async Task<List<IngestedItem>> IngestAsync(string[] sources, IIngestionOptions options, CancellationToken cancellationToken = default)
    {
        var allItems = new List<IngestedItem>();
        
        foreach (string source in sources)
        {
            var items = await IngestAsync(source, options, cancellationToken);
            allItems.AddRange(items);
        }
        
        return allItems;
    }

    /// <summary>
    /// Extracts all text content from a PDF document.
    /// </summary>
    /// <param name="source">Path to the PDF file</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>Complete text content from the PDF</returns>
    private async Task<string> ExtractTextFromPdfAsync(string source, CancellationToken cancellationToken = default)
    {
        var allText = new StringBuilder();
        
        using var document = PdfDocument.Open(source);
        
        foreach (var page in document.GetPages())
        {
            allText.Append(page.Text);
            allText.Append("\n\n"); // Add double newline to separate pages
        }

        var extractedText = allText.ToString();
        
        // Debug: Show extracted text statistics
        Console.WriteLine($"  📄 Extracted {extractedText.Length} characters from PDF");
        Console.WriteLine($"  📄 Text preview: {(extractedText.Length > 200 ? extractedText[..200] + "..." : extractedText)}");

        return await Task.FromResult(extractedText);
    }

    /// <summary>
    /// Processes extracted text using the hybrid paragraph strategy.
    /// This strategy uses fixed-size chunking with overlap for consistent chunk sizes.
    /// </summary>
    /// <param name="text">Raw text extracted from PDF</param>
    /// <param name="source">Original PDF file path</param>
    /// <param name="options">Chunking configuration options</param>
    /// <returns>List of processed text chunks</returns>
    private async Task<List<IngestedItem>> ProcessWithHybridParagraphStrategyAsync(string text, string source, PdfHybridParagraphIngestionOptions options)
    {
        // Return empty list if no text content
        if (string.IsNullOrWhiteSpace(text))
        {
            return await Task.FromResult(new List<IngestedItem>());
        }

        // Use fixed-size chunking with overlap (this is the original working approach)
        var chunks = ChunkingHelper.ChunkTextFixedSize(text, options.MaxSize, options.Overlap);

        // Convert chunks to IngestedItem objects
        var ingestedItems = chunks.Select(chunk => CreateIngestedItem(chunk, source, options)).ToList();

        return ingestedItems;
    }

    /// <summary>
    /// Creates an IngestedItem from a text chunk with appropriate metadata.
    /// </summary>
    /// <param name="chunk">Text chunk to process</param>
    /// <param name="source">Original PDF file path</param>
    /// <param name="options">Configuration options</param>
    /// <returns>IngestedItem with text, metadata, and unique ID</returns>
    private IngestedItem CreateIngestedItem(string chunk, string source, PdfHybridParagraphIngestionOptions options)
    {
        var item = new IngestedItem
        {
            Id = HashingHelper.ComputeSha256Hash(chunk),
            Text = chunk,
            Source = source
        };

        // Add metadata if enabled (matching the original working version)
        if (options.UseMetadata)
        {
            item.Metadata = new Dictionary<string, object>
            {
                ["text"] = chunk,
                ["Length"] = chunk.Length,
                ["source"] = source
            };
        }

        return item;
    }
}