using RAGamuffin.Core;

namespace RAGamuffin.Abstractions;
public interface IIngestionOptions
{
    IngestionStrategy Strategy { get; }
    int MinSize { get; set; }
    int MaxSize { get; set; }
    int Overlap { get; set; }
    bool UseMetadata { get; set; }
}