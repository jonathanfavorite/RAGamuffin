namespace RAGamuffin.Abstractions;
public interface IEmbedderFactory
{
    IEmbedder GetByName(string providerName);
}
