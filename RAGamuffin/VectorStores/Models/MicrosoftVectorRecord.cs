using Microsoft.Extensions.VectorData;

namespace RAGamuffin.VectorStores;
internal class MicrosoftVectorRecord
{
    [VectorStoreKey]
    public string Id { get; set; }

    [VectorStoreData]
    public string? MetaJson { get; set; }

    [VectorStoreVector(768)]
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
