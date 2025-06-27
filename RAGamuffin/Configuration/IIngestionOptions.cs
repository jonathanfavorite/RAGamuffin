using RAGamuffin.Ingestion.Strategies;

namespace RAGamuffin.Configuration;
public interface IIngestionOptions
{
    IngestionStrategy Strategy { get; }
    int MinSize { get; set; }
    int MaxSize { get; set; }
    int Overlap { get; set; }
    bool UseMetadata { get; set; }
}