using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace RAGamuffin.Abstractions;

public interface IVectorStore
{
    Task UpsertAsync(string id, float[] vector, IDictionary<string, object> metaData);
    
    Task<IEnumerable<(string Key, float Score, IDictionary<string, object>? MetaData)>>
    SearchAsync(float[] query, int topK);
    
    Task<IEnumerable<(string Key, float Score, IDictionary<string, object>? MetaData)>>
    SearchAsync(string query, IEmbedder embedder, int topK, CancellationToken cancellationToken = default);
    
    Task<string[]> SearchAndReturnTexts(string query, IEmbedder embedder, int topK, CancellationToken cancellationToken = default);
    
    Task DropCollectionAsync();
    
    // Incremental training methods
    Task<bool> DocumentExistsAsync(string documentId);
    Task<int> GetDocumentCountAsync();
    Task<IEnumerable<string>> GetDocumentIdsAsync();
    Task DeleteDocumentAsync(string documentId);
    Task DeleteDocumentsAsync(IEnumerable<string> documentIds);

    // New metadata retrieval methods
    Task<IDictionary<string, object>?> GetDocumentMetadataAsync(string documentId);
    Task<IEnumerable<(string DocumentId, IDictionary<string, object>? Metadata)>> GetAllDocumentsMetadataAsync();
    Task<IEnumerable<(string DocumentId, IDictionary<string, object>? Metadata)>> GetDocumentsByMetadataFilterAsync(
        string metadataKey, 
        object metadataValue, 
        CancellationToken cancellationToken = default);
    Task<IEnumerable<(string DocumentId, IDictionary<string, object>? Metadata)>> GetDocumentsByMetadataRangeAsync(
        string metadataKey, 
        object minValue, 
        object maxValue, 
        CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetDocumentIdsByMetadataFilterAsync(
        string metadataKey, 
        object metadataValue, 
        CancellationToken cancellationToken = default);
}
