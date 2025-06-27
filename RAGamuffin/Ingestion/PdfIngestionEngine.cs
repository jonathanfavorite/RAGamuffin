using RAGamuffin.Core;
using System.Text;
using UglyToad.PdfPig;

namespace RAGamuffin.Ingestion;
public class PdfIngestionEngine : IIngestionEngine
{
    public async Task<List<IngestedItem>> IngestAsync(string source, IIngestionOptions options, CancellationToken cancellationToken = default)
    {
        string text = await GetAllTextFromDocument(source, cancellationToken);

        if(options is null)
        {
            options = new PdfHybridParagraphIngestionOptions();
        }
        
        switch(options.Strategy)
        {
            case IngestionStrategy.HybridParagraphWithThreshold:
                return await RunHybridParagraphStrategy(text, source, (PdfHybridParagraphIngestionOptions)options);
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
        StringBuilder allText = new StringBuilder();
        using var document = PdfDocument.Open(source);
        foreach (var page in document.GetPages())
        {
            allText.Append(page.Text);
            allText.Append("\n\n");
        }

        return await Task.FromResult(allText.ToString());
    }



    private async Task<List<IngestedItem>> RunHybridParagraphStrategy(string text, string source, PdfHybridParagraphIngestionOptions options)
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

        return items;
    }
}

public class PdfHybridParagraphIngestionOptions : IIngestionOptions
{
    public PDFIngestionMethodology Method { get; set; } = PDFIngestionMethodology.PDFTextExtractor;
    public IngestionStrategy Strategy { get; } = IngestionStrategy.HybridParagraphWithThreshold;
    public int MinSize { get; set; } = 0;
    public int MaxSize { get; set; } = 1200;
    public int Overlap { get; set; } = 500;
    public bool UseMetadata { get; set; } = true;

    public PdfHybridParagraphIngestionOptions() { }

    public PdfHybridParagraphIngestionOptions(int minSize, int maxSize, int overlap, bool useMetadata)
    {
        MinSize = minSize;
        MaxSize = maxSize;
        Overlap = overlap;
        UseMetadata = useMetadata;
    }
}