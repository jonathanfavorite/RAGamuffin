using RAGamuffin.Abstractions;
using RAGamuffin.Core;
using RAGamuffin.Enums;
using RAGamuffin.Helpers;
using UglyToad.PdfPig;
using Microsoft.Extensions.Logging;

namespace RAGamuffin.Ingestion;

/// <summary>
/// Processes PDF documents for RAG (Retrieval-Augmented Generation) systems
/// </summary>
public class PdfIngestionEngine(ILogger<PdfIngestionEngine>? logger = null) : IIngestionEngine
{

    /// <summary>
    /// Ingests a single PDF document into chunks
    /// </summary>
    public async Task<List<IngestedItem>> IngestAsync(
        string source,
        IIngestionOptions? options,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        ValidateSource(source);

        // Use default options if none provided
        options ??= new PdfHybridParagraphIngestionOptions();

        logger?.LogInformation("Starting PDF ingestion for: {Source}", source);

        try
        {
            // Extract all text from PDF
            string text = await ExtractTextFromPdfAsync(source, cancellationToken);

            // Process based on selected strategy
            return await ProcessTextWithStrategyAsync(text, source, options);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to ingest PDF: {Source}", source);
            throw new InvalidOperationException($"Failed to ingest PDF: {source}", ex);
        }
    }

    /// <summary>
    /// Ingests multiple PDF documents
    /// </summary>
    public async Task<List<IngestedItem>> IngestAsync(
        string[] sources,
        IIngestionOptions? options,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        if (sources == null || sources.Length == 0)
        {
            logger?.LogWarning("No sources provided for ingestion");
            return new List<IngestedItem>();
        }

        var allItems = new List<IngestedItem>();
        var errors = new List<string>();

        // Process each PDF individually
        foreach (var source in sources)
        {
            try
            {
                var items = await IngestAsync(source, options, cancellationToken);
                allItems.AddRange(items);
            }
            catch (Exception ex)
            {
                errors.Add($"{source}: {ex.Message}");
                logger?.LogError(ex, "Failed to process file: {Source}", source);
            }
        }

        // Report any errors
        if (errors.Any())
        {
            var errorMessage = $"Failed to process {errors.Count} file(s): {string.Join("; ", errors)}";
            logger?.LogWarning(errorMessage);
        }

        logger?.LogInformation("Processed {SuccessCount} of {TotalCount} files successfully",
            sources.Length - errors.Count, sources.Length);

        return allItems;
    }

    /// <summary>
    /// Validates the source path
    /// </summary>
    private void ValidateSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentNullException(nameof(source), "Source path cannot be null or empty");
        }

        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"PDF file not found: {source}");
        }

        if (!source.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"File must be a PDF: {source}");
        }
    }

    /// <summary>
    /// Extracts all text content from a PDF document
    /// </summary>
    private async Task<string> ExtractTextFromPdfAsync(string source, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var pageTexts = new List<string>();

            using var document = PdfDocument.Open(source);
            var pageCount = document.NumberOfPages;

            logger?.LogDebug("Extracting text from {PageCount} pages", pageCount);

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pageText = page.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    pageTexts.Add(pageText);
                }
            }

            // Join pages with double newline
            var fullText = string.Join("\n\n", pageTexts);

            logger?.LogDebug("Extracted {CharCount} characters from PDF", fullText.Length);
            return fullText;
        }, cancellationToken);
    }

    /// <summary>
    /// Processes text based on the selected strategy
    /// </summary>
    private async Task<List<IngestedItem>> ProcessTextWithStrategyAsync(
        string text,
        string source,
        IIngestionOptions options)
    {
        switch (options.Strategy)
        {
            case IngestionStrategy.HybridParagraphWithThreshold:
                var pdfOptions = options as PdfHybridParagraphIngestionOptions
                    ?? throw new InvalidCastException("Invalid options type for HybridParagraphWithThreshold strategy");
                return await RunHybridParagraphStrategyAsync(text, source, pdfOptions);

            default:
                throw new NotSupportedException($"Ingestion strategy {options.Strategy} is not supported for PDF ingestion.");
        }
    }

    /// <summary>
    /// Chunks text using fixed-size windows with overlap
    /// </summary>
    private async Task<List<IngestedItem>> RunHybridParagraphStrategyAsync(
        string text,
        string source,
        PdfHybridParagraphIngestionOptions options)
    {
        // Skip empty documents
        if (string.IsNullOrWhiteSpace(text))
        {
            logger?.LogWarning("No text content found in PDF: {Source}", source);
            return new List<IngestedItem>();
        }

        // Validate options
        ValidateChunkingOptions(options);

        // Split text into overlapping chunks
        var chunks = ChunkingHelper.ChunkTextFixedSize(text, options.MaxSize, options.Overlap);

        logger?.LogDebug("Created {ChunkCount} chunks from PDF", chunks.Count);

        // Convert chunks to ingested items
        var items = new List<IngestedItem>(chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var item = CreateIngestedItem(chunk, source, i, options);
            items.Add(item);
        }

        return await Task.FromResult(items);
    }

    /// <summary>
    /// Validates chunking options
    /// </summary>
    private void ValidateChunkingOptions(PdfHybridParagraphIngestionOptions options)
    {
        if (options.MaxSize <= 0)
        {
            throw new ArgumentException("MaxSize must be greater than 0", nameof(options));
        }

        if (options.Overlap < 0)
        {
            throw new ArgumentException("Overlap cannot be negative", nameof(options));
        }

        if (options.Overlap >= options.MaxSize)
        {
            throw new ArgumentException("Overlap must be less than MaxSize", nameof(options));
        }
    }

    /// <summary>
    /// Creates an ingested item from a chunk
    /// </summary>
    private IngestedItem CreateIngestedItem(
        string chunk,
        string source,
        int chunkIndex,
        PdfHybridParagraphIngestionOptions options)
    {
        // Generate unique ID from content hash
        var id = HasherHelper.ComputeSha256Hash(chunk);

        var item = new IngestedItem
        {
            Id = id,
            Text = chunk,
            Source = source
        };

        // Add metadata if enabled
        if (options.UseMetadata)
        {
            item.Metadata = BuildMetadata(chunk, source, chunkIndex);
        }

        return item;
    }

    /// <summary>
    /// Builds metadata dictionary for a chunk
    /// </summary>
    private Dictionary<string, object> BuildMetadata(string chunk, string source, int chunkIndex)
    {
        return new Dictionary<string, object>
        {
            ["text"] = chunk,
            ["length"] = chunk.Length,
            ["source"] = source,
            ["chunkIndex"] = chunkIndex,
            ["ingestionDate"] = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Configuration for PDF chunking using hybrid paragraph strategy
/// </summary>
public class PdfHybridParagraphIngestionOptions : IIngestionOptions
{
    private int _maxSize = 800;
    private int _overlap = 400;

    /// <summary>
    /// PDF extraction method (default: PDFTextExtractor)
    /// </summary>
    public PDFIngestionMethodology Method { get; set; } = PDFIngestionMethodology.PDFTextExtractor;

    /// <summary>
    /// Chunking strategy type
    /// </summary>
    public IngestionStrategy Strategy { get; } = IngestionStrategy.HybridParagraphWithThreshold;

    /// <summary>
    /// Minimum chunk size in characters (unused in current implementation)
    /// </summary>
    public int MinSize { get; set; } = 0;

    /// <summary>
    /// Maximum chunk size in characters
    /// </summary>
    public int MaxSize
    {
        get => _maxSize;
        set
        {
            if (value <= 0)
                throw new ArgumentException("MaxSize must be greater than 0");
            _maxSize = value;
        }
    }

    /// <summary>
    /// Character overlap between consecutive chunks
    /// </summary>
    public int Overlap
    {
        get => _overlap;
        set
        {
            if (value < 0)
                throw new ArgumentException("Overlap cannot be negative");
            _overlap = value;
        }
    }

    /// <summary>
    /// Whether to include metadata with chunks
    /// </summary>
    public bool UseMetadata { get; set; } = true;

    /// <summary>
    /// Creates options with default values
    /// </summary>
    public PdfHybridParagraphIngestionOptions() { }

    /// <summary>
    /// Creates options with custom values
    /// </summary>
    public PdfHybridParagraphIngestionOptions(int minSize, int maxSize, int overlap, bool useMetadata)
    {
        MinSize = minSize;
        MaxSize = maxSize;
        Overlap = overlap;
        UseMetadata = useMetadata;
    }

    /// <summary>
    /// Validates that the current options are valid
    /// </summary>
    public bool IsValid(out string? errorMessage)
    {
        errorMessage = null;

        if (MaxSize <= 0)
        {
            errorMessage = "MaxSize must be greater than 0";
            return false;
        }

        if (Overlap < 0)
        {
            errorMessage = "Overlap cannot be negative";
            return false;
        }

        if (Overlap >= MaxSize)
        {
            errorMessage = "Overlap must be less than MaxSize";
            return false;
        }

        return true;
    }
}