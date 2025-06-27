namespace RAGamuffin.Embedding;
public interface IEmbedder
{
    int Dimension { get; }
    string ProviderName { get; set; }
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
    Task<float[][]> EmbedAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
}
