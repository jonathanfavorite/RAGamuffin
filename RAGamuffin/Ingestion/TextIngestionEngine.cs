using RAGamuffin.Abstractions;
using RAGamuffin.Core;
using RAGamuffin.Enums;
using RAGamuffin.Helpers;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RAGamuffin.Ingestion;

/// <summary>
/// Processes text documents for RAG (Retrieval-Augmented Generation) systems
/// </summary>
public class TextIngestionEngine(ILogger<TextIngestionEngine>? logger = null) : IIngestionEngine
{
    /// <summary>
    /// Ingests a single text document into chunks
    /// </summary>
    public async Task<List<IngestedItem>> IngestAsync(
        string source,
        IIngestionOptions? options,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        ValidateSource(source);

        // Use default options if none provided
        options ??= new TextHybridParagraphIngestionOptions();

        logger?.LogInformation("Starting text ingestion for: {Source}", source);

        try
        {
            // Read all text from file
            string text = await ReadTextFromFileAsync(source, cancellationToken);

            // Process based on selected strategy
            return await ProcessTextWithStrategyAsync(text, source, options);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogError(ex, "Failed to ingest text file: {Source}", source);
            throw new InvalidOperationException($"Failed to ingest text file: {source}", ex);
        }
    }

    /// <summary>
    /// Ingests multiple text documents
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

        // Process each file individually
        foreach (var source in sources)
        {
            try
            {
                var items = await IngestAsync(source, options, cancellationToken);
                allItems.AddRange(items);
            }
            catch (OperationCanceledException)
            {
                logger?.LogInformation("Ingestion cancelled");
                throw;
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
    /// Validates the source path and file type
    /// </summary>
    private void ValidateSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentNullException(nameof(source), "Source path cannot be null or empty");
        }

        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"Text file not found: {source}");
        }
    }

    /// <summary>
    /// Reads text content from file with encoding detection
    /// </summary>
    private async Task<string> ReadTextFromFileAsync(string source, CancellationToken cancellationToken)
    {
        try
        {
            // Try UTF-8 first (most common)
            var text = await File.ReadAllTextAsync(source, Encoding.UTF8, cancellationToken);

            var fileInfo = new FileInfo(source);
            logger?.LogDebug("Read {ByteCount} bytes ({CharCount} characters) from {FileName}",
                fileInfo.Length, text.Length, fileInfo.Name);

            return text;
        }
        catch (DecoderFallbackException)
        {
            // Fallback to default encoding if UTF-8 fails
            logger?.LogWarning("UTF-8 decoding failed for {Source}, trying default encoding", source);
            return await File.ReadAllTextAsync(source, Encoding.Default, cancellationToken);
        }
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
                var textOptions = options as TextHybridParagraphIngestionOptions
                    ?? throw new InvalidCastException("Invalid options type for HybridParagraphWithThreshold strategy");
                return await RunHybridParagraphStrategyAsync(text, source, textOptions);

            default:
                throw new NotSupportedException($"Ingestion strategy {options.Strategy} is not supported for text ingestion");
        }
    }

    /// <summary>
    /// Chunks text using fixed-size windows with overlap
    /// </summary>
    private async Task<List<IngestedItem>> RunHybridParagraphStrategyAsync(
        string text,
        string source,
        TextHybridParagraphIngestionOptions options)
    {
        // Skip empty documents
        if (string.IsNullOrWhiteSpace(text))
        {
            logger?.LogWarning("No text content found in file: {Source}", source);
            return new List<IngestedItem>();
        }

        // Validate options
        ValidateChunkingOptions(options);

        // Apply text preprocessing based on method
        text = PreprocessText(text, options.Method);

        // Split text into overlapping chunks
        var chunks = ChunkingHelper.ChunkTextFixedSize(text, options.MaxSize, options.Overlap);

        // Filter chunks by minimum size if specified
        if (options.MinSize > 0)
        {
            chunks = chunks.Where(c => c.Length >= options.MinSize).ToList();
            logger?.LogDebug("Filtered to {ChunkCount} chunks meeting minimum size of {MinSize}",
                chunks.Count, options.MinSize);
        }

        logger?.LogDebug("Created {ChunkCount} chunks from text file", chunks.Count);

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
    /// Preprocesses text based on ingestion method
    /// </summary>
    private string PreprocessText(string text, TextIngestionMethodology method)
    {
        switch (method)
        {
            case TextIngestionMethodology.PlainText:
                // Normalize line endings and trim
                return text.Replace("\r\n", "\n").Trim();

            // Add more preprocessing methods as needed
            default:
                return text;
        }
    }

    /// <summary>
    /// Validates chunking options
    /// </summary>
    private void ValidateChunkingOptions(TextHybridParagraphIngestionOptions options)
    {
        if (options.MaxSize <= 0)
        {
            throw new ArgumentException("MaxSize must be greater than 0", nameof(options));
        }

        if (options.MinSize < 0)
        {
            throw new ArgumentException("MinSize cannot be negative", nameof(options));
        }

        if (options.MinSize > options.MaxSize)
        {
            throw new ArgumentException("MinSize cannot be greater than MaxSize", nameof(options));
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
        TextHybridParagraphIngestionOptions options)
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
            item.Metadata = BuildMetadata(chunk, source, chunkIndex, options.Method);
        }

        return item;
    }

    /// <summary>
    /// Builds metadata dictionary for a chunk
    /// </summary>
    private Dictionary<string, object> BuildMetadata(
        string chunk,
        string source,
        int chunkIndex,
        TextIngestionMethodology method)
    {
        var fileName = Path.GetFileName(source);

        return new Dictionary<string, object>
        {
            ["text"] = chunk,
            ["length"] = chunk.Length,
            ["source"] = source,
            ["fileName"] = fileName,
            ["chunkIndex"] = chunkIndex,
            ["ingestionMethod"] = method.ToString(),
            ["ingestionDate"] = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Configuration for text file chunking using hybrid paragraph strategy
/// </summary>
public class TextHybridParagraphIngestionOptions : IIngestionOptions
{
    private int _minSize = 0;
    private int _maxSize = 800;
    private int _overlap = 400;

    /// <summary>
    /// Text preprocessing method (default: PlainText)
    /// </summary>
    public TextIngestionMethodology Method { get; set; } = TextIngestionMethodology.PlainText;

    /// <summary>
    /// Chunking strategy type
    /// </summary>
    public IngestionStrategy Strategy { get; } = IngestionStrategy.HybridParagraphWithThreshold;

    /// <summary>
    /// Minimum chunk size in characters (chunks smaller than this are filtered out)
    /// </summary>
    public int MinSize
    {
        get => _minSize;
        set
        {
            if (value < 0)
                throw new ArgumentException("MinSize cannot be negative");
            _minSize = value;
        }
    }

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
    public TextHybridParagraphIngestionOptions() { }

    /// <summary>
    /// Creates options with custom values
    /// </summary>
    public TextHybridParagraphIngestionOptions(int minSize, int maxSize, int overlap, bool useMetadata)
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

        if (MinSize < 0)
        {
            errorMessage = "MinSize cannot be negative";
            return false;
        }

        if (MinSize > MaxSize)
        {
            errorMessage = "MinSize cannot be greater than MaxSize";
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