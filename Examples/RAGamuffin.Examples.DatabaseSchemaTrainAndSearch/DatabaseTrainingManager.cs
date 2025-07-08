using RAGamuffin.Abstractions;
using RAGamuffin.Builders;
using RAGamuffin.Core;
using RAGamuffin.Embedding;
using RAGamuffin.Enums;
using RAGamuffin.VectorStores;

namespace RAGamuffin.Examples.DatabaseSchemaTrainAndSearch;

public class DatabaseTrainingManager
{
    private readonly string _dbDetailsLocation;
    private readonly string _modelPath;
    private readonly string _tokenizerPath;
    private readonly string _vectorDbPath;
    private readonly string _collectionName;

    public DatabaseTrainingManager(
        string dbDetailsLocation,
        string modelPath,
        string tokenizerPath,
        string vectorDbPath,
        string collectionName)
    {
        _dbDetailsLocation = dbDetailsLocation;
        _modelPath = modelPath;
        _tokenizerPath = tokenizerPath;
        _vectorDbPath = vectorDbPath;
        _collectionName = collectionName;
    }

    public async Task<RAGamuffinModel> TrainDatabaseSchemaAsync(bool performTraining = true)
    {
        var tableInfoArr = GetAllTablesAndColumns(_dbDetailsLocation + "ready.json");
        Console.WriteLine($"Loaded {tableInfoArr.Count} tables from JSON file");

        // Check for the specific table we're looking for
        var targetTable = tableInfoArr.FirstOrDefault(t => t.TableName.Equals("dbo.tblPAY_EMPLOYEES", StringComparison.OrdinalIgnoreCase));
        if (targetTable != null)
        {
            Console.WriteLine($"✓ Found dbo.tblPAY_EMPLOYEES in JSON with {targetTable.Columns.Count} columns");
        }
        else
        {
            Console.WriteLine("✗ dbo.tblPAY_EMPLOYEES NOT found in JSON file");
            // Look for similar table names
            var similarTables = tableInfoArr.Where(t => t.TableName.ToLowerInvariant().Contains("employee")).Take(5).ToList();
            if (similarTables.Count > 0)
            {
                Console.WriteLine("Similar employee tables found:");
                foreach (var table in similarTables)
                {
                    Console.WriteLine($"  - {table.TableName} ({table.Columns.Count} columns)");
                }
            }
        }

        IEmbedder embedder = new OnnxEmbedder(_modelPath, _tokenizerPath);
        SqliteDatabaseModel database = new(_vectorDbPath, _collectionName);
        
        Console.WriteLine($"Embedder dimension: {embedder.Dimension}");

        RAGamuffinModel model;

        if (performTraining)
        {
            var builder = new IngestionTrainingBuilder()
                .WithVectorDatabase(database)
                .WithEmbeddingModel(embedder)
                .WithTrainingStrategy(TrainingStrategy.RetrainFromScratch);

            List<TextItem> textToTrainWith = new();

            // Create comprehensive table descriptions
            var processedTables = 0;
            var skippedTables = 0;

            Console.WriteLine($"\nProcessing {tableInfoArr.Count} tables...");

            foreach (var table in tableInfoArr)
            {
                try
                {
                    // Special debugging for the table we're looking for
                    if (table.TableName.Equals("dbo.tblPAY_EMPLOYEES", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"\n🔍 SPECIAL DEBUG: Processing target table {table.TableName}");
                        Console.WriteLine($"  Description: {table.Description}");
                        Console.WriteLine($"  Columns: {table.Columns?.Count ?? 0}");
                        Console.WriteLine($"  RowCount: {table.RowCount}");
                    }

                    var items = CreateTextItemFromTableInfo(table);
                    
                    // Special debugging for the table we're looking for
                    if (table.TableName.Equals("dbo.tblPAY_EMPLOYEES", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"🔍 SPECIAL DEBUG: Created {items.Count} items for {table.TableName}");
                        foreach (var item in items)
                        {
                            Console.WriteLine($"  - Item ID: {item.Id}, DocType: {item.Metadata?.GetValueOrDefault("DocType", "Unknown")}");
                        }
                    }

                    if (items.Count > 0)
                    {
                        textToTrainWith.AddRange(items);
                        processedTables++;

                        if (processedTables % 100 == 0)
                        {
                            Console.WriteLine($"Processed {processedTables} tables...");
                        }
                    }
                    else
                    {
                        skippedTables++;
                        if (skippedTables <= 5) // Only show first few skipped tables
                        {
                            Console.WriteLine($"Skipped table: {table.TableName} (no items created)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    skippedTables++;
                    Console.WriteLine($"Error processing table {table.TableName}: {ex.Message}");
                    
                    // Special debugging for the table we're looking for
                    if (table.TableName.Equals("dbo.tblPAY_EMPLOYEES", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"🔍 SPECIAL DEBUG: ERROR processing target table {table.TableName}");
                        Console.WriteLine($"  Exception: {ex}");
                    }
                }
            }

            Console.WriteLine($"\nProcessing complete:");
            Console.WriteLine($"  Processed tables: {processedTables}");
            Console.WriteLine($"  Skipped tables: {skippedTables}");
            Console.WriteLine($"  Total text items: {textToTrainWith.Count}");

            // Add source-of-truth analysis
            var sourceOfTruthItems = CreateSourceOfTruthAnalysis(tableInfoArr);
            textToTrainWith.AddRange(sourceOfTruthItems);

            // Add natural language query training items
            var naturalLanguageItems = CreateNaturalLanguageTrainingItems(tableInfoArr);
            textToTrainWith.AddRange(naturalLanguageItems);

            // Calculate and display training summary
            var tableItems = textToTrainWith.Where(item => item.Metadata?.ContainsKey("DocType") == true && item.Metadata["DocType"].ToString() == "table").Count();
            var columnItems = textToTrainWith.Where(item => item.Metadata?.ContainsKey("DocType") == true && item.Metadata["DocType"].ToString() == "column").Count();
            var sourceOfTruthItemsCount = sourceOfTruthItems.Count;
            var naturalLanguageItemsCount = naturalLanguageItems.Count;
            var otherItems = textToTrainWith.Count - tableItems - columnItems - sourceOfTruthItemsCount - naturalLanguageItemsCount;

            Console.WriteLine($"\n=== TRAINING SUMMARY ===");
            Console.WriteLine($"Total items to train: {textToTrainWith.Count:N0}");
            Console.WriteLine($"  - Table items: {tableItems:N0}");
            Console.WriteLine($"  - Column items: {columnItems:N0}");
            Console.WriteLine($"  - Source-of-truth analysis items: {sourceOfTruthItemsCount:N0}");
            Console.WriteLine($"  - Natural language items: {naturalLanguageItemsCount:N0}");
            Console.WriteLine($"  - Other items: {otherItems:N0}");

            var totalContentLength = textToTrainWith.Sum(item => item.Content?.Length ?? 0);
            Console.WriteLine($"Total content length: {totalContentLength:N0} characters");
            var avgContentLength = textToTrainWith.Count > 0 ? totalContentLength / textToTrainWith.Count : 0;
            Console.WriteLine($"Average content length per item: {avgContentLength:N0} characters");
            var expectedDbSize = textToTrainWith.Count * 768.0 / 1024 / 1024;
            Console.WriteLine($"Expected database size: ~{expectedDbSize:F1} MB (assuming 768-dim vectors)");
            Console.WriteLine(new string('=', 50));

            // Validate all text items before training
            Console.WriteLine($"\nValidating {textToTrainWith.Count} text items...");
            for (int i = 0; i < textToTrainWith.Count; i++)
            {
                var item = textToTrainWith[i];
                if (string.IsNullOrEmpty(item.Content))
                {
                    Console.WriteLine($"ERROR: TextItem at index {i} has null or empty content. ID: {item.Id}");
                    textToTrainWith.RemoveAt(i);
                    i--; // Adjust index after removal
                }
                else
                {
                    Console.WriteLine($"Item {i}: ID={item.Id}, Content length={item.Content.Length}");
                }
            }

            var final = textToTrainWith.ToArray();
            Console.WriteLine($"\nFinal array has {final.Length} items");
            
            // Special debugging: Check if our target table is in the final array
            var targetTableItems = final.Where(item => item.Id.Equals("dbo.tblPAY_EMPLOYEES", StringComparison.OrdinalIgnoreCase)).ToList();
            Console.WriteLine($"🔍 SPECIAL DEBUG: Target table items in final array: {targetTableItems.Count}");
            foreach (var item in targetTableItems)
            {
                var docType = item.Metadata?.ContainsKey("DocType") == true ? item.Metadata["DocType"].ToString() : "Unknown";
                Console.WriteLine($"  - Item ID: {item.Id}, DocType: {docType}");
            }
            
            var trainResult = await builder.TrainWithText(final);
            var ingestedItems = trainResult.IngestedItems;
            model = trainResult.Model;

            Console.WriteLine($"Successfully trained with {ingestedItems.Count} items");
            Console.WriteLine($"Items chunked: {ingestedItems.Count(i => i.Metadata.ContainsKey("chunked") && (bool)i.Metadata["chunked"])}");
            Console.WriteLine($"Items not chunked: {ingestedItems.Count(i => i.Metadata.ContainsKey("chunked") && !(bool)i.Metadata["chunked"])}");
            
            // Special debugging: Check if our target table was ingested
            var ingestedTargetItems = ingestedItems.Where(item => item.Id.Equals("dbo.tblPAY_EMPLOYEES", StringComparison.OrdinalIgnoreCase)).ToList();
            Console.WriteLine($"🔍 SPECIAL DEBUG: Target table items in ingested items: {ingestedTargetItems.Count}");
            foreach (var item in ingestedTargetItems)
            {
                var docType = item.Metadata?.ContainsKey("DocType") == true ? item.Metadata["DocType"].ToString() : "Unknown";
                Console.WriteLine($"  - Item ID: {item.Id}, DocType: {docType}");
            }
        }
        else
        {
            // Just load the existing model for searching
            Console.WriteLine("Skipping training - loading existing model for search...");
            model = new RAGamuffinModel(embedder, new SqliteVectorStoreProvider(database.SqliteDbPath, database.CollectionName, embedder.Dimension), TrainingStrategy.ProcessOnly, new Dictionary<string, IIngestionOptions>());

            var documentCount = await model.GetDocumentCount();
            Console.WriteLine($"Loaded model with {documentCount} existing documents");
        }

        return model;
    }

    private List<TextItem> CreateTextItemFromTableInfo(TableInfo table)
    {
        List<TextItem> textItems = new List<TextItem>();

        // Debug: Log table processing start
        Console.WriteLine($"DEBUG: Processing table: '{table.TableName}'");

        // CRITICAL: Ensure we always process every table, even with minimal data
        if (string.IsNullOrEmpty(table.TableName))
        {
            Console.WriteLine($"ERROR: Table name is null or empty for table: {table}");
            // Even if table name is null, try to create a basic item
            var fallbackItem = new TextItem("UNKNOWN_TABLE", "Unknown table with no name");
            fallbackItem.Metadata = new() { ["DocType"] = "table", ["Priority"] = "primary" };
            fallbackItem.SkipChunking = true;
            textItems.Add(fallbackItem);
            return textItems;
        }

        // Ensure we have a description, even if it's basic
        if (string.IsNullOrEmpty(table.Description))
        {
            Console.WriteLine($"WARNING: Table description is null or empty for table: {table.TableName}");
            table.Description = $"Table {table.TableName}"; // Provide default description
        }

        // Debug: Log table basic info
        Console.WriteLine($"DEBUG: Table '{table.TableName}' - Description: '{table.Description}', Columns: {table.Columns?.Count ?? 0}");

        // 1. PRIMARY: Create comprehensive table description with all columns
        var comprehensiveDescription = BuildComprehensiveTableDescription(table);

        // CRITICAL: If description is empty, create a minimal one
        if (string.IsNullOrEmpty(comprehensiveDescription))
        {
            Console.WriteLine($"WARNING: Comprehensive description is empty for table: {table.TableName}, creating minimal description");
            comprehensiveDescription = $"Table: {table.TableName}\nDescription: {table.Description}\nColumns: {table.Columns?.Count ?? 0}";
        }

        Console.WriteLine($"DEBUG: Created comprehensive description for '{table.TableName}' - Length: {comprehensiveDescription.Length}");

        TextItem tableItem = new(table.TableName, comprehensiveDescription);
        tableItem.Metadata = new()
        {
            ["TableName"] = table.TableName,
            ["DocType"] = "table",
            ["RowCount"] = table.RowCount,
            ["ColumnCount"] = table.Columns?.Count ?? 0,
            ["HasPrimaryKeys"] = table.PrimaryKeys?.Count > 0,
            ["HasForeignKeys"] = table.ForeignKeys?.Count > 0,
            ["HasIndexes"] = table.Indexes?.Count > 0,
            ["HasTriggers"] = table.Triggers?.Count > 0,
            ["ContentLength"] = comprehensiveDescription.Length,
            ["Priority"] = "primary"
        };
        tableItem.SkipChunking = true;

        Console.WriteLine($"Created comprehensive table item: {table.TableName} with content length: {comprehensiveDescription.Length}");

        // 2. SECONDARY: Create individual items for ALL columns (if any exist)
        if (table.Columns != null && table.Columns.Count > 0)
        {
            Console.WriteLine($"DEBUG: Processing {table.Columns.Count} columns for table '{table.TableName}'");

            foreach (var column in table.Columns)
            {
                Console.WriteLine($"DEBUG: Processing column: '{column.ColumnName}' for table '{table.TableName}'");

                if (string.IsNullOrEmpty(column.ColumnName))
                {
                    Console.WriteLine($"ERROR: Column name is null or empty for table: {table.TableName}");
                    continue;
                }

                if (string.IsNullOrEmpty(column.Description))
                {
                    Console.WriteLine($"WARNING: Column description is null or empty for column: {table.TableName}.{column.ColumnName}");
                    column.Description = $"Column {column.ColumnName}";
                }

                // Create richer column description with table context
                var columnDescription = BuildKeyColumnDescription(table, column);
                TextItem columnItem = new($"{table.TableName}.{column.ColumnName}", columnDescription);

                columnItem.Metadata = new()
                {
                    ["TableName"] = table.TableName,
                    ["ColumnName"] = column.ColumnName,
                    ["DataType"] = column.DataType,
                    ["IsNullable"] = column.IsNullable,
                    ["DefaultValue"] = column.DefaultValue,
                    ["MaxLength"] = column.MaxLength?.ToString() ?? "",
                    ["IsIdentity"] = column.IsIdentity,
                    ["DocType"] = "column",
                    ["Priority"] = "secondary",
                    ["IsPrimaryKey"] = table.PrimaryKeys?.Contains(column.ColumnName) ?? false,
                    ["IsForeignKey"] = table.ForeignKeys?.Any(fk => fk.Column == column.ColumnName) ?? false
                };

                columnItem.SkipChunking = true;

                Console.WriteLine($"Created column item: {table.TableName}.{column.ColumnName} with content length: {columnDescription.Length}");

                textItems.Add(columnItem);
            }
        }
        else
        {
            Console.WriteLine($"DEBUG: No columns found for table '{table.TableName}', skipping column items");
        }

        textItems.Add(tableItem);

        Console.WriteLine($"DEBUG: Successfully created {textItems.Count} text items for table '{table.TableName}' (1 table + {textItems.Count - 1} columns)");

        return textItems;
    }

    private string BuildComprehensiveTableDescription(TableInfo table)
    {
        var description = new System.Text.StringBuilder();

        // Defensive programming: handle null collections
        var columns = table.Columns ?? new List<ColumnInfo>();
        var primaryKeys = table.PrimaryKeys ?? new List<string>();
        var foreignKeys = table.ForeignKeys ?? new List<ForeignKeyInfo>();
        var indexes = table.Indexes ?? new List<IndexInfo>();
        var triggers = table.Triggers ?? new List<TriggerInfo>();

        // Start with natural language description
        description.AppendLine($"Table: {table.TableName}");
        description.AppendLine($"Description: {table.Description}");
        
        // Add natural language context for common operations
        var tableNameLower = table.TableName.ToLowerInvariant();
        if (tableNameLower.Contains("employee"))
        {
            description.AppendLine("Purpose: This table stores employee information and is used for employee management, payroll processing, and HR operations.");
            description.AppendLine("Common Operations: Insert new employees, update employee details, retrieve employee records for payroll, HR reporting.");
        }
        else if (tableNameLower.Contains("pay") || tableNameLower.Contains("salary"))
        {
            description.AppendLine("Purpose: This table handles payment and salary information for employees.");
            description.AppendLine("Common Operations: Process payroll, calculate salaries, track payment history, generate pay reports.");
        }
        else if (tableNameLower.Contains("customer") || tableNameLower.Contains("client"))
        {
            description.AppendLine("Purpose: This table stores customer or client information.");
            description.AppendLine("Common Operations: Add new customers, update customer details, retrieve customer records for billing and support.");
        }
        
        description.AppendLine($"Row Count: {table.RowCount:N0}");

        // Show key columns first with natural language descriptions
        var keyColumns = IdentifyKeyColumns(table);
        if (keyColumns.Count > 0)
        {
            description.AppendLine($"Key Columns ({keyColumns.Count}):");
            foreach (var column in keyColumns)
            {
                var columnDesc = $"{column.ColumnName}: {column.DataType} - {column.Description}";
                if (column.IsIdentity)
                    columnDesc += " (auto-generated)";
                if (primaryKeys.Contains(column.ColumnName))
                    columnDesc += " (primary key)";
                description.AppendLine($"  - {columnDesc}");
            }
        }

        // Show all columns with more natural language
        if (columns.Count > 0)
        {
            description.AppendLine($"All Columns ({columns.Count}):");
            foreach (var column in columns)
            {
                var columnInfo = $"{column.ColumnName}: {column.DataType}";
                if (column.MaxLength.HasValue && column.MaxLength.Value > 0)
                    columnInfo += $"({column.MaxLength})";
                else if (column.Precision.HasValue && column.Scale.HasValue)
                    columnInfo += $"({column.Precision},{column.Scale})";
                else if (column.Precision.HasValue)
                    columnInfo += $"({column.Precision})";

                if (!column.IsNullable)
                    columnInfo += " NOT NULL";
                if (column.IsIdentity)
                    columnInfo += " IDENTITY";
                if (!string.IsNullOrEmpty(column.DefaultValue))
                    columnInfo += $" DEFAULT {column.DefaultValue}";

                description.AppendLine($"  - {columnInfo} - {column.Description}");
            }
        }

        // Show relationships
        if (primaryKeys.Count > 0)
        {
            description.AppendLine($"Primary Keys: {string.Join(", ", primaryKeys)}");
        }

        if (foreignKeys.Count > 0)
        {
            description.AppendLine($"Foreign Keys:");
            foreach (var fk in foreignKeys)
            {
                description.AppendLine($"  - {fk.Column} -> {fk.ReferencedTable}.{fk.ReferencedColumn}");
            }
        }

        if (indexes.Count > 0)
        {
            description.AppendLine($"Indexes ({indexes.Count}):");
            foreach (var index in indexes.Take(5)) // Limit to first 5 indexes
            {
                description.AppendLine($"  - {index.IndexName}: {string.Join(", ", index.Columns)}");
            }
            if (indexes.Count > 5)
                description.AppendLine($"  ... and {indexes.Count - 5} more indexes");
        }

        if (triggers.Count > 0)
        {
            description.AppendLine($"Triggers ({triggers.Count}):");
            foreach (var trigger in triggers.Take(3)) // Limit to first 3 triggers
            {
                description.AppendLine($"  - {trigger.TriggerName}: {trigger.TriggerType}");
            }
            if (triggers.Count > 3)
                description.AppendLine($"  ... and {triggers.Count - 3} more triggers");
        }

        return description.ToString();
    }

    private List<ColumnInfo> IdentifyKeyColumns(TableInfo table)
    {
        var keyColumns = new List<ColumnInfo>();

        // Defensive programming: handle null collections
        var columns = table.Columns ?? new List<ColumnInfo>();
        var primaryKeys = table.PrimaryKeys ?? new List<string>();
        var foreignKeys = table.ForeignKeys ?? new List<ForeignKeyInfo>();

        Console.WriteLine($"DEBUG: Identifying key columns for table '{table.TableName}'");
        Console.WriteLine($"DEBUG: Total columns: {columns.Count}, Primary keys: {primaryKeys.Count}, Foreign keys: {foreignKeys.Count}");

        // Primary keys (always important)
        foreach (var pkName in primaryKeys)
        {
            var column = columns.FirstOrDefault(c => c.ColumnName?.Equals(pkName, StringComparison.OrdinalIgnoreCase) == true);
            if (column != null)
            {
                keyColumns.Add(column);
                Console.WriteLine($"DEBUG: Added primary key column: {column.ColumnName}");
            }
            else
            {
                Console.WriteLine($"WARNING: Primary key '{pkName}' not found in columns for table '{table.TableName}'");
            }
        }

        // Foreign keys (important for relationships)
        foreach (var fk in foreignKeys)
        {
            var column = columns.FirstOrDefault(c => c.ColumnName?.Equals(fk.Column, StringComparison.OrdinalIgnoreCase) == true);
            if (column != null && !keyColumns.Any(kc => kc.ColumnName?.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase) == true))
            {
                keyColumns.Add(column);
                Console.WriteLine($"DEBUG: Added foreign key column: {column.ColumnName} -> {fk.ReferencedTable}.{fk.ReferencedColumn}");
            }
            else if (column == null)
            {
                Console.WriteLine($"WARNING: Foreign key column '{fk.Column}' not found in columns for table '{table.TableName}'");
            }
        }

        // Identity columns (important for inserts)
        var identityColumns = columns.Where(c => c.IsIdentity).ToList();
        foreach (var column in identityColumns)
        {
            if (!keyColumns.Any(kc => kc.ColumnName?.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase) == true))
                keyColumns.Add(column);
        }

        // Commonly searched columns (heuristic-based)
        var commonSearchPatterns = new[] { "name", "id", "code", "number", "date", "email", "phone", "address" };
        foreach (var column in columns)
        {
            if (keyColumns.Any(kc => kc.ColumnName?.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase) == true))
                continue;

            var columnNameLower = column.ColumnName?.ToLowerInvariant() ?? "";
            if (commonSearchPatterns.Any(pattern => columnNameLower.Contains(pattern)))
            {
                keyColumns.Add(column);
            }
        }

        // Limit to reasonable number (max 10 key columns per table)
        var finalKeyColumns = keyColumns.Take(10).ToList();
        Console.WriteLine($"DEBUG: Final key columns for '{table.TableName}': {finalKeyColumns.Count} (limited from {keyColumns.Count})");
        return finalKeyColumns;
    }

    private string BuildKeyColumnDescription(TableInfo table, ColumnInfo column)
    {
        var description = new System.Text.StringBuilder();

        // Defensive programming: handle null collections
        var primaryKeys = table.PrimaryKeys ?? new List<string>();
        var foreignKeys = table.ForeignKeys ?? new List<ForeignKeyInfo>();

        description.AppendLine($"Column: {table.TableName}.{column.ColumnName}");
        description.AppendLine($"Table: {table.TableName} - {table.Description}");
        description.AppendLine($"Column Description: {column.Description}");
        description.AppendLine($"Data Type: {column.DataType}");

        if (column.MaxLength.HasValue && column.MaxLength.Value > 0)
            description.AppendLine($"Max Length: {column.MaxLength}");
        else if (column.Precision.HasValue && column.Scale.HasValue)
            description.AppendLine($"Precision: {column.Precision},{column.Scale}");
        else if (column.Precision.HasValue)
            description.AppendLine($"Precision: {column.Precision}");

        if (column.IsNullable)
            description.AppendLine("Nullable: Yes");
        else
            description.AppendLine("Nullable: No");

        if (column.IsIdentity)
            description.AppendLine("Identity: Yes (auto-incrementing)");

        if (column.IsComputed)
            description.AppendLine("Computed: Yes");

        if (!string.IsNullOrEmpty(column.DefaultValue))
            description.AppendLine($"Default Value: {column.DefaultValue}");

        // Show relationships
        if (primaryKeys.Contains(column.ColumnName))
            description.AppendLine("Role: Primary Key");

        var foreignKey = foreignKeys.FirstOrDefault(fk => fk.Column == column.ColumnName);
        if (foreignKey != null)
            description.AppendLine($"Foreign Key: References {foreignKey.ReferencedTable}.{foreignKey.ReferencedColumn}");

        // Show column stats if available
        if (column.Stats != null)
        {
            description.AppendLine($"Column Statistics:");
            if (column.Stats.DistinctValues.HasValue)
                description.AppendLine($"  Distinct Values: {column.Stats.DistinctValues:N0}");
            if (column.Stats.NullCount.HasValue)
                description.AppendLine($"  Null Count: {column.Stats.NullCount:N0}");
            if (column.Stats.MinValue != null)
                description.AppendLine($"  Min Value: {column.Stats.MinValue}");
            if (column.Stats.MaxValue != null)
                description.AppendLine($"  Max Value: {column.Stats.MaxValue}");
        }

        return description.ToString();
    }

    private List<TextItem> CreateSourceOfTruthAnalysis(List<TableInfo> allTables)
    {
        var textItems = new List<TextItem>();

        // Analyze shared columns across tables to identify source-of-truth relationships
        var sharedColumns = AnalyzeSharedColumns(allTables);

        if (sharedColumns.Count > 0)
        {
            var analysisContent = BuildSourceOfTruthAnalysis(sharedColumns, allTables);
            var analysisItem = new TextItem("source_of_truth_analysis", analysisContent);
            analysisItem.Metadata = new()
            {
                ["DocType"] = "source_of_truth_analysis",
                ["AnalysisType"] = "cross_table_relationships",
                ["SharedColumnCount"] = sharedColumns.Count
            };
            analysisItem.SkipChunking = true;

            textItems.Add(analysisItem);
            Console.WriteLine($"Created source-of-truth analysis with {sharedColumns.Count} shared column patterns");
        }

        return textItems;
    }

    private Dictionary<string, List<(string TableName, string ColumnName)>> AnalyzeSharedColumns(List<TableInfo> allTables)
    {
        var sharedColumns = new Dictionary<string, List<(string TableName, string ColumnName)>>();

        // Find columns with similar names across tables
        foreach (var table in allTables)
        {
            foreach (var column in table.Columns)
            {
                // Normalize column name for comparison (remove common prefixes/suffixes)
                var normalizedName = NormalizeColumnName(column.ColumnName);

                if (!sharedColumns.ContainsKey(normalizedName))
                    sharedColumns[normalizedName] = new List<(string, string)>();

                sharedColumns[normalizedName].Add((table.TableName, column.ColumnName));
            }
        }

        // Filter to only columns that appear in multiple tables
        return sharedColumns.Where(kvp => kvp.Value.Count > 1)
                           .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private string NormalizeColumnName(string columnName)
    {
        // Remove common prefixes and suffixes, convert to lowercase
        var normalized = columnName.ToLowerInvariant();

        // Remove common prefixes
        var prefixes = new[] { "tbl", "fld", "col", "id", "pk", "fk" };
        foreach (var prefix in prefixes)
        {
            if (normalized.StartsWith(prefix))
                normalized = normalized.Substring(prefix.Length);
        }

        // Remove common suffixes
        var suffixes = new[] { "id", "key", "pk", "fk", "num", "no", "code" };
        foreach (var suffix in suffixes)
        {
            if (normalized.EndsWith(suffix))
                normalized = normalized.Substring(0, normalized.Length - suffix.Length);
        }

        return normalized.Trim();
    }

    private string BuildSourceOfTruthAnalysis(Dictionary<string, List<(string TableName, string ColumnName)>> sharedColumns, List<TableInfo> allTables)
    {
        var analysis = new System.Text.StringBuilder();
        analysis.AppendLine("SOURCE OF TRUTH ANALYSIS");
        analysis.AppendLine("========================");
        analysis.AppendLine();
        analysis.AppendLine("This analysis identifies columns that appear across multiple tables and suggests potential source-of-truth relationships.");
        analysis.AppendLine();

        foreach (var kvp in sharedColumns.OrderByDescending(x => x.Value.Count))
        {
            var normalizedName = kvp.Key;
            var occurrences = kvp.Value;

            analysis.AppendLine($"Shared Column Pattern: '{normalizedName}' (appears in {occurrences.Count} tables)");

            // Group by table to show all columns for each table
            var tableGroups = occurrences.GroupBy(x => x.TableName).ToList();

            foreach (var tableGroup in tableGroups)
            {
                var tableName = tableGroup.Key;
                var table = allTables.FirstOrDefault(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));

                analysis.AppendLine($"  Table: {tableName}");
                if (table != null)
                {
                    analysis.AppendLine($"    Description: {table.Description}");
                    analysis.AppendLine($"    Row Count: {table.RowCount:N0}");

                    // Check if this table has identity columns (potential source of truth)
                    var identityColumns = table.Columns.Where(c => c.IsIdentity).ToList();
                    if (identityColumns.Count > 0)
                    {
                        analysis.AppendLine($"    Identity Columns: {string.Join(", ", identityColumns.Select(c => c.ColumnName))}");
                    }

                    // Check if this table has primary keys
                    if (table.PrimaryKeys.Count > 0)
                    {
                        analysis.AppendLine($"    Primary Keys: {string.Join(", ", table.PrimaryKeys)}");
                    }
                }

                foreach (var (_, columnName) in tableGroup)
                {
                    var column = table?.Columns.FirstOrDefault(c => c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
                    analysis.AppendLine($"    Column: {columnName}");
                    if (column != null)
                    {
                        analysis.AppendLine($"      Type: {column.DataType}");
                        analysis.AppendLine($"      Description: {column.Description}");
                        if (column.IsIdentity)
                            analysis.AppendLine($"      *** IDENTITY COLUMN - LIKELY SOURCE OF TRUTH ***");
                    }
                }
                analysis.AppendLine();
            }

            // Suggest source of truth based on heuristics
            var suggestions = SuggestSourceOfTruth(occurrences, allTables);
            if (!string.IsNullOrEmpty(suggestions))
            {
                analysis.AppendLine($"  SUGGESTED SOURCE OF TRUTH: {suggestions}");
            }

            analysis.AppendLine(new string('-', 80));
            analysis.AppendLine();
        }

        return analysis.ToString();
    }

    private string SuggestSourceOfTruth(List<(string TableName, string ColumnName)> occurrences, List<TableInfo> allTables)
    {
        var suggestions = new List<string>();

        foreach (var (tableName, columnName) in occurrences)
        {
            var table = allTables.FirstOrDefault(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
            if (table == null) continue;

            var column = table.Columns.FirstOrDefault(c => c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
            if (column == null) continue;

            // Heuristic: Identity columns are likely sources of truth
            if (column.IsIdentity)
            {
                suggestions.Add($"{tableName}.{columnName} (IDENTITY)");
            }

            // Heuristic: Tables with "master" or "main" in name might be sources of truth
            if (tableName.ToLowerInvariant().Contains("master") || tableName.ToLowerInvariant().Contains("main"))
            {
                suggestions.Add($"{tableName}.{columnName} (MASTER TABLE)");
            }

            // Heuristic: Tables with "employee" in name for employee-related data
            if (tableName.ToLowerInvariant().Contains("employee") && columnName.ToLowerInvariant().Contains("employee"))
            {
                suggestions.Add($"{tableName}.{columnName} (EMPLOYEE MASTER)");
            }
        }

        return string.Join(", ", suggestions);
    }

    private List<TableInfo> GetAllTablesAndColumns(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath))
        {
            Console.WriteLine($"ERROR: JSON file not found: {jsonFilePath}");
            return new List<TableInfo>();
        }

        try
        {
            var jsonContent = File.ReadAllText(jsonFilePath);
            var tableInfoArr = System.Text.Json.JsonSerializer.Deserialize<List<TableInfo>>(jsonContent);
            return tableInfoArr ?? new List<TableInfo>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to deserialize JSON file: {ex.Message}");
            return new List<TableInfo>();
        }
    }

    private List<TextItem> CreateNaturalLanguageTrainingItems(List<TableInfo> tableInfoArr)
    {
        var textItems = new List<TextItem>();

        Console.WriteLine("Creating natural language training items...");

        foreach (var table in tableInfoArr)
        {
            if (string.IsNullOrEmpty(table.TableName))
                continue;

            var tableNameLower = table.TableName.ToLowerInvariant();
            var columns = table.Columns ?? new List<ColumnInfo>();

            // Create natural language descriptions for common operations
            if (tableNameLower.Contains("employee"))
            {
                // Employee table specific natural language items
                var employeeInsertItem = new TextItem(
                    $"nl_insert_employee_{table.TableName}",
                    $"To insert a new employee, use the {table.TableName} table. This table contains employee information including personal details, job information, and payroll data. When inserting a new employee, you typically need to provide: FirstName, LastName, EmployeeID (if not auto-generated), and other required fields based on your business rules."
                );
                employeeInsertItem.Metadata = new()
                {
                    ["TableName"] = table.TableName,
                    ["DocType"] = "natural_language",
                    ["Operation"] = "insert",
                    ["Entity"] = "employee",
                    ["Priority"] = "high"
                };
                employeeInsertItem.SkipChunking = true;
                textItems.Add(employeeInsertItem);

                var employeeUpdateItem = new TextItem(
                    $"nl_update_employee_{table.TableName}",
                    $"To update employee information, use the {table.TableName} table. This table stores all employee records and allows you to modify employee details such as name, address, job title, salary, and other personal information."
                );
                employeeUpdateItem.Metadata = new()
                {
                    ["TableName"] = table.TableName,
                    ["DocType"] = "natural_language",
                    ["Operation"] = "update",
                    ["Entity"] = "employee",
                    ["Priority"] = "high"
                };
                employeeUpdateItem.SkipChunking = true;
                textItems.Add(employeeUpdateItem);
            }

            if (tableNameLower.Contains("pay") || tableNameLower.Contains("salary"))
            {
                // Payroll table specific natural language items
                var payrollItem = new TextItem(
                    $"nl_payroll_{table.TableName}",
                    $"The {table.TableName} table handles payroll and payment information. This table is used for processing employee salaries, calculating pay, tracking payment history, and generating payroll reports. It contains payment records, salary information, and payroll processing data."
                );
                payrollItem.Metadata = new()
                {
                    ["TableName"] = table.TableName,
                    ["DocType"] = "natural_language",
                    ["Operation"] = "payroll",
                    ["Entity"] = "payment",
                    ["Priority"] = "high"
                };
                payrollItem.SkipChunking = true;
                textItems.Add(payrollItem);
            }

            // Generic table operations
            var insertItem = new TextItem(
                $"nl_insert_{table.TableName}",
                $"To insert new records into the {table.TableName} table, you need to provide values for the required columns. This table is used for storing {table.Description.ToLower()}. The table contains {columns.Count} columns and currently has {table.RowCount:N0} rows of data."
            );
            insertItem.Metadata = new()
            {
                ["TableName"] = table.TableName,
                ["DocType"] = "natural_language",
                ["Operation"] = "insert",
                ["Entity"] = "generic",
                ["Priority"] = "medium"
            };
            insertItem.SkipChunking = true;
            textItems.Add(insertItem);

            var selectItem = new TextItem(
                $"nl_select_{table.TableName}",
                $"To retrieve data from the {table.TableName} table, you can query this table to get information about {table.Description.ToLower()}. This table contains {columns.Count} columns including key fields that are important for data retrieval and reporting."
            );
            selectItem.Metadata = new()
            {
                ["TableName"] = table.TableName,
                ["DocType"] = "natural_language",
                ["Operation"] = "select",
                ["Entity"] = "generic",
                ["Priority"] = "medium"
            };
            selectItem.SkipChunking = true;
            textItems.Add(selectItem);

            // Create items for key columns that are commonly used in operations
            var keyColumns = IdentifyKeyColumns(table);
            foreach (var column in keyColumns.Take(3)) // Limit to top 3 key columns
            {
                var columnItem = new TextItem(
                    $"nl_column_{table.TableName}_{column.ColumnName}",
                    $"The {column.ColumnName} column in the {table.TableName} table is a key field used for {column.Description.ToLower()}. This column has data type {column.DataType} and is {(column.IsNullable ? "nullable" : "required")}. {(column.IsIdentity ? "This column auto-generates values." : "")}"
                );
                columnItem.Metadata = new()
                {
                    ["TableName"] = table.TableName,
                    ["ColumnName"] = column.ColumnName,
                    ["DocType"] = "natural_language",
                    ["Operation"] = "column_info",
                    ["Entity"] = "column",
                    ["Priority"] = "medium"
                };
                columnItem.SkipChunking = true;
                textItems.Add(columnItem);
            }
        }

        Console.WriteLine($"Created {textItems.Count} natural language training items");
        return textItems;
    }
} 