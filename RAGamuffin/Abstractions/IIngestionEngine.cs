using RAGamuffin.Core;

namespace RAGamuffin.Abstractions;
public interface IIngestionEngine
{
    Task<List<IngestedItem>> IngestAsync(string source, IIngestionOptions options, CancellationToken cancellationToken = default);
    Task<List<IngestedItem>> IngestAsync(string[] sources, IIngestionOptions options, CancellationToken cancellationToken = default);
}
