namespace RAGamuffin.Core;

/// <summary>
/// Represents a piece of text content that can be processed directly by the training pipeline
/// without requiring a file. Perfect for real-time data ingestion scenarios.
/// </summary>
public class TextItem
{
    /// <summary>
    /// Unique identifier for this text item
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The text content to be processed and added to the vector store
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this text item was created/received
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Optional metadata to associate with this text item
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    public TextItem()
    {
        Metadata = new Dictionary<string, object>();
    }

    public TextItem(string id, string content)
    {
        Id = id;
        Content = content;
        Timestamp = DateTime.Now;
        Metadata = new Dictionary<string, object>();
    }

    public TextItem(string id, string content, DateTime timestamp)
    {
        Id = id;
        Content = content;
        Timestamp = timestamp;
        Metadata = new Dictionary<string, object>();
    }

    public TextItem(string id, string content, DateTime timestamp, Dictionary<string, object>? metadata)
    {
        Id = id;
        Content = content;
        Timestamp = timestamp;
        Metadata = metadata ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Gets a metadata value of the specified type, or default if not found
    /// </summary>
    /// <typeparam name="T">The expected type of the metadata value</typeparam>
    /// <param name="key">The metadata key</param>
    /// <returns>The metadata value or default(T) if not found</returns>
    public T? GetMetadata<T>(string key)
    {
        if (Metadata?.TryGetValue(key, out var value) == true)
        {
            if (value is T typedValue)
                return typedValue;
            
            // Try to convert if possible
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }
        }
        return default;
    }

    /// <summary>
    /// Sets a metadata value
    /// </summary>
    /// <param name="key">The metadata key</param>
    /// <param name="value">The metadata value</param>
    public void SetMetadata(string key, object value)
    {
        Metadata ??= new Dictionary<string, object>();
        Metadata[key] = value;
    }

    /// <summary>
    /// Checks if a metadata key exists
    /// </summary>
    /// <param name="key">The metadata key to check</param>
    /// <returns>True if the key exists, false otherwise</returns>
    public bool HasMetadata(string key)
    {
        return Metadata?.ContainsKey(key) == true;
    }

    /// <summary>
    /// Gets all metadata keys
    /// </summary>
    /// <returns>Array of metadata keys</returns>
    public string[] GetMetadataKeys()
    {
        return Metadata?.Keys.ToArray() ?? Array.Empty<string>();
    }
} 