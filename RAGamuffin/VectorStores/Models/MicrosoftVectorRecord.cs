using Microsoft.Extensions.VectorData;

namespace RAGamuffin.VectorStores.Models;
internal class MicrosoftVectorRecord
{
    [VectorStoreKey]
    public string Id { get; set; }

    [VectorStoreData]
    public string? MetaJson { get; set; }

    [VectorStoreVector(768)] // Dimension will be set dynamically when the collection is created
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
