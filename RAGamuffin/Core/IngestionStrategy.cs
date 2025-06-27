namespace RAGamuffin.Core;
public enum IngestionStrategy
{
    ParagraphSplitting,
    FixedSizeSlidingWindow,
    HybridParagraphWithThreshold,
    ContentDefinedChunking,
}
