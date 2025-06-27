using System.Text;

namespace RAGamuffin.Common;

/// <summary>
/// Utility class for text chunking operations.
/// Provides methods for splitting text into smaller, manageable chunks with configurable overlap.
/// </summary>
public static class ChunkingHelper
{
    /// <summary>
    /// Splits text into fixed-size chunks with specified overlap.
    /// This method ensures that no text is lost and maintains context continuity between chunks.
    /// </summary>
    /// <param name="text">The text to split into chunks</param>
    /// <param name="chunkSize">The maximum size of each chunk in characters</param>
    /// <param name="overlap">The number of characters to overlap between consecutive chunks</param>
    /// <returns>A list of text chunks</returns>
    /// <exception cref="ArgumentNullException">Thrown when text is null</exception>
    /// <exception cref="ArgumentException">Thrown when chunkSize or overlap is invalid</exception>
    public static List<string> ChunkTextFixedSize(string text, int chunkSize = 800, int overlap = 200)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text), "Text cannot be null.");
        }

        if (chunkSize <= 0)
        {
            throw new ArgumentException("Chunk size must be greater than zero.", nameof(chunkSize));
        }

        if (overlap < 0)
        {
            throw new ArgumentException("Overlap cannot be negative.", nameof(overlap));
        }

        if (overlap >= chunkSize)
        {
            throw new ArgumentException("Overlap must be less than chunk size.", nameof(overlap));
        }

        // Handle empty text
        if (string.IsNullOrEmpty(text))
        {
            return new List<string>();
        }

        // Handle text shorter than chunk size
        if (text.Length <= chunkSize)
        {
            return new List<string> { text };
        }

        var chunks = new List<string>();
        int startIndex = 0;

        while (startIndex < text.Length)
        {
            // Calculate the end index for this chunk
            int endIndex = Math.Min(startIndex + chunkSize, text.Length);
            
            // Extract the chunk
            string chunk = text.Substring(startIndex, endIndex - startIndex);
            chunks.Add(chunk);

            // Break if we've reached the end of the text
            if (endIndex == text.Length)
            {
                break;
            }

            // Move to the next chunk position with overlap
            startIndex += chunkSize - overlap;
        }

        return chunks;
    }

    /// <summary>
    /// Splits text into chunks based on paragraph boundaries with size constraints.
    /// This method attempts to keep paragraphs together while respecting maximum chunk size.
    /// </summary>
    /// <param name="text">The text to split into chunks</param>
    /// <param name="maxChunkSize">The maximum size of each chunk in characters</param>
    /// <param name="paragraphSeparators">Array of strings that indicate paragraph boundaries</param>
    /// <returns>A list of text chunks</returns>
    /// <exception cref="ArgumentNullException">Thrown when text is null</exception>
    /// <exception cref="ArgumentException">Thrown when maxChunkSize is invalid</exception>
    public static List<string> ChunkTextByParagraphs(string text, int maxChunkSize = 1000, string[]? paragraphSeparators = null)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text), "Text cannot be null.");
        }

        if (maxChunkSize <= 0)
        {
            throw new ArgumentException("Maximum chunk size must be greater than zero.", nameof(maxChunkSize));
        }

        // Use default paragraph separators if none provided
        paragraphSeparators ??= new[] { "\n\n", "\r\n\r\n", "\n\r\n\r" };

        // Handle empty text
        if (string.IsNullOrEmpty(text))
        {
            return new List<string>();
        }

        // Handle text shorter than max chunk size
        if (text.Length <= maxChunkSize)
        {
            return new List<string> { text };
        }

        var chunks = new List<string>();
        var paragraphs = text.Split(paragraphSeparators, StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            var trimmedParagraph = paragraph.Trim();
            
            if (string.IsNullOrEmpty(trimmedParagraph))
            {
                continue;
            }

            // If adding this paragraph would exceed the limit, save current chunk and start new one
            if (currentChunk.Length + trimmedParagraph.Length > maxChunkSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
            }

            // Add paragraph to current chunk
            if (currentChunk.Length > 0)
            {
                currentChunk.Append("\n\n");
            }
            currentChunk.Append(trimmedParagraph);
        }

        // Add the last chunk if it has content
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        return chunks;
    }

    /// <summary>
    /// Validates chunking parameters to ensure they are reasonable for text processing.
    /// </summary>
    /// <param name="chunkSize">The proposed chunk size</param>
    /// <param name="overlap">The proposed overlap size</param>
    /// <returns>True if parameters are valid, false otherwise</returns>
    public static bool ValidateChunkingParameters(int chunkSize, int overlap)
    {
        return chunkSize > 0 && 
               overlap >= 0 && 
               overlap < chunkSize && 
               chunkSize <= 10000; // Reasonable upper limit
    }
}
