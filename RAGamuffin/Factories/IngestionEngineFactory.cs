using RAGamuffin.Abstractions;
using RAGamuffin.Ingestion;

namespace RAGamuffin.Factories;
public class IngestionEngineFactory : IIngestionEngineFactory
{
    public IIngestionEngine CreateEngine(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        return extension switch
        {
            ".pdf" => new PdfIngestionEngine(),
            _ => new TextIngestionEngine()
        };
    }

    public IIngestionEngine CreateEngine(string[] filePaths)
    {
        var groupedFiles = filePaths.GroupBy(Path.GetExtension).ToDictionary(g => g.Key.ToLowerInvariant(), g => g.ToArray());
        
        if (groupedFiles.Count == 1)
        {
            var extension = groupedFiles.Keys.First();
            return extension switch
            {
                ".pdf" => new PdfIngestionEngine(),
                _ => new TextIngestionEngine()
            };
        }
        
        throw new InvalidOperationException("Multiple file types detected. Use MultiFileIngestionManager for mixed file types.");
    }

    public bool CanHandleFileType(string filePath)
    {
        return true;
    }
} 