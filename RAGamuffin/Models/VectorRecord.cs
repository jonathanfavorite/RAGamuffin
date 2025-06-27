using Microsoft.Extensions.VectorData;

namespace RAGamuffin.Models;
public class VectorRecord
{
    [VectorStoreKey]
    public string Id { get; set; }

    [VectorStoreData]
    public string? MetaJson { get; set; }

    [VectorStoreVector(768)]
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
