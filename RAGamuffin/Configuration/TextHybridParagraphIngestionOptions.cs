using RAGamuffin.Configuration;
using RAGamuffin.Ingestion.Strategies;

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