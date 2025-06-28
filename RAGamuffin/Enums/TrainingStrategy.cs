namespace RAGamuffin.Enums;

public enum TrainingStrategy
{
    /// <summary>
    /// Drop all existing data and retrain from scratch
    /// </summary>
    RetrainFromScratch,
    
    /// <summary>
    /// Add new documents to existing vector store (skip if document already exists)
    /// </summary>
    IncrementalAdd,
    
    /// <summary>
    /// Add new documents and update existing ones (replace if document exists)
    /// </summary>
    IncrementalUpdate,
    
    /// <summary>
    /// Only process documents, don't perform vector operations
    /// </summary>
    ProcessOnly
} 