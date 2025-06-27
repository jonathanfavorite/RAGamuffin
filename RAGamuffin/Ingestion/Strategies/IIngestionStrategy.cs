namespace RAGamuffin.Ingestion.Strategies;
public enum IngestionStrategy
{
    ParagraphSplitting,
    FixedSizeSlidingWindow,
    HybridParagraphWithThreshold,
    ContentDefinedChunking,
}
