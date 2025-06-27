using RAGamuffin.Core;
using System.Text;
using UglyToad.PdfPig;

namespace RAGamuffin.Ingestion;
public class TextIngestionEngine : IIngestionEngine
{
    public async Task<List<IngestedItem>> IngestAsync(string source, IIngestionOptions options, CancellationToken cancellationToken = default)
    {
        string text = await GetAllTextFromDocument(source, cancellationToken);

        if (options is null)
        {
            options = new TextHybridParagraphIngestionOptions();
        }

        switch (options.Strategy)
        {
            case IngestionStrategy.HybridParagraphWithThreshold:
                return await RunHybridParagraphStrategy(text, source, (TextHybridParagraphIngestionOptions)options);
            default:
                throw new NotSupportedException($"Ingestion strategy {options.Strategy} is not supported for PDF ingestion.");
        }
    }

    public async Task<List<IngestedItem>> IngestAsync(string[] sources, IIngestionOptions options, CancellationToken cancellationToken = default)
    {
        List<IngestedItem> allItems = new();
        foreach (var source in sources)
        {
            var items = await IngestAsync(source, options, cancellationToken);
            allItems.AddRange(items);
        }
        return allItems;
    }

    private async Task<string> GetAllTextFromDocument(string source, CancellationToken cancellationToken = default)
    {
        return await File.ReadAllTextAsync(source, cancellationToken);
    }

    private async Task<List<IngestedItem>> RunHybridParagraphStrategy(string text, string source, TextHybridParagraphIngestionOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return await Task.FromResult(new List<IngestedItem>());
        }

        // Use fixed-size chunking with overlap
        var chunks = ChunkingHelper.ChunkTextFixedSize(text, options.MaxSize, options.Overlap);

        // Map each text chunk to an IngestedItem
        var items = chunks.Select(chunk =>
        {
            var id = HasherHelper.ComputeSha256Hash(chunk);
            var item = new IngestedItem
            {
                Id = id,
                Text = chunk,
                Source = source
            };

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
        }).ToList();

        return await Task.FromResult(items);
    }
}

public class TextHybridParagraphIngestionOptions : IIngestionOptions
{
    public TextIngestionMethodology Method { get; set; } = TextIngestionMethodology.PlainText;
    public IngestionStrategy Strategy { get; } = IngestionStrategy.HybridParagraphWithThreshold;
    public int MinSize { get; set; } = 500;
    public int MaxSize { get; set; } = 1000;
    public int Overlap { get; set; } = 200;
    public bool UseMetadata { get; set; } = true;

    public TextHybridParagraphIngestionOptions() { }

    public TextHybridParagraphIngestionOptions(int minSize, int maxSize, int overlap, bool useMetadata)
    { 
        MinSize = minSize;
        MaxSize = maxSize;
        Overlap = overlap;
        UseMetadata = useMetadata;
    }
}