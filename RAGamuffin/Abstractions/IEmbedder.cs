namespace RAGamuffin.Abstractions;
public interface IEmbedder
{
    int Dimension { get; }
    string ProviderName { get; set; }
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
