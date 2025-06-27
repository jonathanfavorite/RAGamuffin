namespace RAGamuffin.Embedding;
public interface IEmbedderFactory
{
    IEmbedder GetByName(string providerName);
}
