using RAGamuffin.Abstractions;

namespace RAGamuffin.Factories;
internal class EmbedderFactory : IEmbedderFactory
{
    public IEmbedder GetByName(string providerName)
    {
        throw new NotImplementedException();
    }
}
