using RAGamuffin.Abstractions;
using RAGamuffin.Core;
using RAGamuffin.Enums;
using RAGamuffin.Factories;
using RAGamuffin.Helpers;

namespace RAGamuffin.Ingestion;
public class MultiFileIngestionManager
{
    private readonly IIngestionEngineFactory _factory;
    private readonly IEmbedder _embedder;
    private readonly IVectorStore _vectorStore;
    private readonly TrainingStrategy _trainingStrategy;
    private readonly Dictionary<string, IIngestionOptions> _fileTypeOptions;

    public MultiFileIngestionManager(IIngestionEngineFactory factory, IEmbedder embedder, IVectorStore vectorStore, TrainingStrategy trainingStrategy = TrainingStrategy.RetrainFromScratch)
    {
        _factory = factory;
        _embedder = embedder;
        _vectorStore = vectorStore;
        _trainingStrategy = trainingStrategy;
        _fileTypeOptions = new Dictionary<string, IIngestionOptions>();
    }

    public MultiFileIngestionManager WithFileTypeOptions(string fileExtension, IIngestionOptions options)
    {
        _fileTypeOptions[fileExtension.ToLowerInvariant()] = options;
        return this;
    }

    public async Task<List<IngestedItem>> IngestFilesAsync(string[] filePaths, CancellationToken cancellationToken = default)
    {
        return await IngestFilesAsync(filePaths, true, cancellationToken);
    }

    public async Task<List<IngestedItem>> IngestFilesAsync(string[] filePaths, bool performVectorOperations, CancellationToken cancellationToken = default)
    {
        if (performVectorOperations && _trainingStrategy == TrainingStrategy.RetrainFromScratch)
        {
            await _vectorStore.DropCollectionAsync();
        }

        var groupedFiles = filePaths.GroupBy(Path.GetExtension).ToDictionary(g => g.Key.ToLowerInvariant(), g => g.ToArray());
        var allItems = new List<IngestedItem>();

        foreach (var group in groupedFiles)
        {
            var extension = group.Key;
            var files = group.Value;

            IIngestionOptions options;
            if (extension == ".pdf" && _fileTypeOptions.TryGetValue(".pdf", out var pdfOptions))
            {
                options = pdfOptions;
            }
            else if (_fileTypeOptions.TryGetValue("*", out var textOptions))
            {
                options = textOptions;
            }
            else
            {
                options = GetDefaultOptionsForFileType(extension);
            }

            var engine = _factory.CreateEngine(files);
            var items = await engine.IngestAsync(files, options, cancellationToken);
            
            if (performVectorOperations)
            {
                foreach (var item in items)
                {
                    var embedding = await _embedder.EmbedAsync(item.Text, cancellationToken);
                    await _vectorStore.UpsertAsync(item.Id, embedding, item.Metadata);
                }
            }
            
            allItems.AddRange(items);
        }

        return allItems;
    }

    private IIngestionOptions GetDefaultOptionsForFileType(string extension)
    {
        return extension switch
        {
            ".pdf" => new PdfHybridParagraphIngestionOptions(),
            _ => new TextHybridParagraphIngestionOptions()
        };
    }
} 