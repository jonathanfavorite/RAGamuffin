namespace RAGamuffin.Helpers;
internal static class ChunkingHelper
{
    internal static List<string> ChunkTextFixedSize(string text, int chunkSize = 800, int overlap = 200)
    {
        var chunks = new List<string>();
        int start = 0;
        while (start < text.Length)
        {
            int end = Math.Min(start + chunkSize, text.Length);
            string chunk = text.Substring(start, end - start);
            chunks.Add(chunk);
            if (end == text.Length) break;
            start += chunkSize - overlap;
        }
        return chunks;
    }
}
