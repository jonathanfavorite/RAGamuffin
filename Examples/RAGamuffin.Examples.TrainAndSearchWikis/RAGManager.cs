using RAGamuffin.Abstractions;
using RAGamuffin.Builders;
using RAGamuffin.Core;
using RAGamuffin.Embedding;
using RAGamuffin.Ingestion;

namespace RAGamuffin.Examples.TrainAndSearchWikis;
internal class RAGManager
{
    private IEmbedder _embedder;
    private string _databasePath;
    private string _embeddingModelPath;
    private string _embeddingTokenizerPath;
    private RAGamuffinModel _RAGamuffinModel;
    public RAGManager(string databasePath, string embeddingModelPath, string embeddingTokenizerPath)
    {
        _databasePath = databasePath;
        _embeddingModelPath = embeddingModelPath;
        _embeddingTokenizerPath = embeddingTokenizerPath;

        _embedder = new OnnxEmbedder(_embeddingModelPath, _embeddingTokenizerPath);

        _RAGamuffinModel = new IngestionTrainingBuilder()
            .WithVectorDatabase(new SqliteDatabaseModel(_databasePath, "wikis"))
            .WithEmbeddingModel(_embedder)
            .WithTextOptions(new TextHybridParagraphIngestionOptions
            {
                MaxSize = 500,
                MinSize = 100,
                Overlap = 100,
            })
            .WithVectorSize(768)
            .WithTrainingFiles(new string[] { }) // No initial files, will be set during training
            .WithTrainingStrategy(RAGamuffin.Enums.TrainingStrategy.RetrainFromScratch)
            .Build();
    }
    public async Task TrainFiles(string filesDirectory, string[] fileTypes)
    {
        string[] filesToTrain = GetFilesToTrain(filesDirectory, fileTypes);
        if (filesToTrain.Length == 0)
        {
            throw new InvalidOperationException("No files found to train with the specified file types.");
        }

        Console.WriteLine($"[Info] Found {filesToTrain.Length} files to train.");

        List<TextItem> textItems = await ConvertToTrainingTextItems(filesToTrain);

        Console.WriteLine("[Info] Converting files to TextItems for training.");

        if (textItems.Count == 0)
        {
            throw new InvalidOperationException("No valid text items found to train.");
        }

        Console.WriteLine($"[Info] Training with {textItems.Count} text items.");

        await _RAGamuffinModel.TrainWithText(textItems.ToArray());

        Console.WriteLine("Done!");
    }

    public async Task<string> Search(string query, int resultCount = 5)
    {
        // Print the query embedding vector (first 10 values)
        if (_embedder is RAGamuffin.Embedding.OnnxEmbedder onnxEmbedder)
        {
            var queryEmbedding = await onnxEmbedder.EmbedAsync(query);
            //Console.WriteLine("[DEBUG] Query embedding (first 10 values): " + string.Join(", ", queryEmbedding.Take(10)));
        }

        var results = await _RAGamuffinModel.Search(query, resultCount);
        int i = 1;
        foreach (var result in results)
        {
            string text = result.MetaData != null && result.MetaData.ContainsKey("text") ? result.MetaData["text"].ToString() ?? "" : "<no text>";
            //Console.WriteLine($"[DEBUG] Result #{i}: Score={result.Score:F4}\n{text.Substring(0, Math.Min(200, text.Length)).Replace("\n", " ")}\n---");
            i++;
        }

        return string.Join("\r\n", await _RAGamuffinModel.SearchAndReturnTexts(query, resultCount));
    }

    private string[] GetFilesToTrain(string filesDirectory, string[] fileTypes)
    {
        if (!Directory.Exists(filesDirectory))
            throw new DirectoryNotFoundException($"The directory {filesDirectory} does not exist.");

        return Directory.GetFiles(filesDirectory, "*.*", SearchOption.AllDirectories)
            .Where(file => fileTypes.Contains(Path.GetExtension(file).ToLowerInvariant()))
            .ToArray();
    }

    private async Task<List<TextItem>> ConvertToTrainingTextItems(string[] files)
    {
        string repoName = @"winteam-wiki-repo\";

        List<TextItem> textItems = new List<TextItem>();
        bool printedSSOChunk = false;
        foreach (string file in files)
        {
            // read the contents
            string[] removeLeftOfRepoName = file.Split(repoName, StringSplitOptions.RemoveEmptyEntries);
            if (removeLeftOfRepoName.Length == 0)
            {
                Console.WriteLine($"[Warning] File {file} does not contain the expected repository name '{repoName}'. Skipping.");
                continue;
            }
            string contents = await File.ReadAllTextAsync(file);
            TextItem item = new(removeLeftOfRepoName[1], contents);
            item.SetMetadata("fileName", Path.GetFileName(file));
            item.SkipChunking = true; // Disable chunking for all items
            textItems.Add(item);

            // Print a sample chunk containing 'SSO' or 'AlloweHubSSO' for verification
            if (!printedSSOChunk && (contents.Contains("SSO") || contents.Contains("AlloweHubSSO")))
            {
                Console.WriteLine("[DEBUG] Sample chunk containing 'SSO' or 'AlloweHubSSO':\n" + contents.Substring(0, Math.Min(500, contents.Length)) + "\n---");
                printedSSOChunk = true;
            }
        }

        return textItems;
    }
}
