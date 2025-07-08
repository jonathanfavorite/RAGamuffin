using RAGamuffin.Core;

namespace RAGamuffin.Examples.DatabaseSchemaTrainAndSearch;

public class TableInfo
{
    public string TableName { get; set; } = "";
    public string Description { get; set; } = "";
    public long RowCount { get; set; }
    public List<ColumnInfo> Columns { get; set; } = new();
    public List<string> PrimaryKeys { get; set; } = new();
    public List<ForeignKeyInfo> ForeignKeys { get; set; } = new();
    public List<IndexInfo> Indexes { get; set; } = new();
    public List<TriggerInfo> Triggers { get; set; } = new();
    public string Purpose { get; set; } = "";
    public string CommonOperations { get; set; } = "";
}

public class ColumnInfo
{
    public string ColumnName { get; set; } = "";
    public string DataType { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsNullable { get; set; }
    public string? DefaultValue { get; set; }
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsComputed { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public ColumnStats? Stats { get; set; }
}

public class ColumnStats
{
    public long? DistinctValues { get; set; }
    public long? NullCount { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
}

public class ForeignKeyInfo
{
    public string Column { get; set; } = "";
    public string ReferencedTable { get; set; } = "";
    public string ReferencedColumn { get; set; } = "";
}

public class IndexInfo
{
    public string IndexName { get; set; } = "";
    public List<string> Columns { get; set; } = new();
    public bool IsUnique { get; set; }
    public bool IsClustered { get; set; }
}

public class TriggerInfo
{
    public string TriggerName { get; set; } = "";
    public string TriggerType { get; set; } = "";
    public string EventType { get; set; } = "";
}

/// <summary>
/// Represents the analysis of a search query to determine intent and optimal search strategy
/// </summary>
public class QueryAnalysis
{
    public string Intent { get; set; } = "";
    public string Operation { get; set; } = "";
    public string TargetEntity { get; set; } = "";
}

/// <summary>
/// Represents a search result with applied boosting for intelligent ranking
/// </summary>
public class BoostedSearchResult
{
    public (string Key, float Score, IDictionary<string, object> MetaData) OriginalResult { get; set; }
    public float BoostedScore { get; set; }
    public float BoostApplied { get; set; }
    public string BoostReason { get; set; } = "";
} 