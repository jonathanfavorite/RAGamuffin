namespace RAGamuffin.Core;
public class OnnxEmbeddingModel
{
    public string ModelPath { get; set; }
    public string TokenizerPath { get; set; }

    public OnnxEmbeddingModel(string modelPath, string tokenizerPath)
    {
        ModelPath = modelPath;
        tokenizerPath = tokenizerPath;
    }
}
