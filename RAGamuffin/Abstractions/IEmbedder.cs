namespace RAGamuffin.Abstractions;
public interface IEmbedder
{
    int Dimension { get; }
    string ProviderName { get; set; }
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Embeds multiple texts in a single batch operation for much better performance
    /// </summary>
    /// <param name="texts">Array of texts to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of embeddings, one for each input text</returns>
    Task<float[][]> EmbedBatchAsync(string[] texts, CancellationToken cancellationToken = default);
}
