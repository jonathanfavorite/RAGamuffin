using RAGamuffin.Configuration;
using RAGamuffin.Ingestion.Strategies;

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