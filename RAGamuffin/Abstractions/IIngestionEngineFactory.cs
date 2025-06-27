using RAGamuffin.Abstractions;

namespace RAGamuffin.Abstractions;
public interface IIngestionEngineFactory
{
    IIngestionEngine CreateEngine(string filePath);
    IIngestionEngine CreateEngine(string[] filePaths);
    bool CanHandleFileType(string filePath);
} 