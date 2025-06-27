namespace RAGamuffin.Core;
public class IngestedItem
{
    public string Id { get; set; }
    public string Text { get; set; }
    public string Source { get; set; }
    public int Tokens => Text.Length / 4;
    public IDictionary<string, object> Metadata { get; set; }
    public float[] Vectors { get; set; } = [];
}
