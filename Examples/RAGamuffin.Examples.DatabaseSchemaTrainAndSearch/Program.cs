using RAGamuffin.Examples.DatabaseSchemaTrainAndSearch;

const string DB_DETAILS_LOCATION = @"C:\test\ww\";
const string MODEL_PATH = @"C:\RAGamuffin\model.onnx";
const string TOKENIZER_PATH = @"C:\RAGamuffin\tokenizer.json";
const string VECTOR_DB_PATH = @"C:\RAGamuffin\winteam_database_copy.db";
const string COLLECTION_NAME = "WinteamTablesAndColumns";

// Set this to false to skip training and just load the model for searching
const bool PERFORM_TRAINING = false;

try
{
    // Initialize the training manager
    var trainingManager = new DatabaseTrainingManager(
        DB_DETAILS_LOCATION,
        MODEL_PATH,
        TOKENIZER_PATH,
        VECTOR_DB_PATH,
        COLLECTION_NAME
    );

    // Train or load the model
    var model = await trainingManager.TrainDatabaseSchemaAsync(PERFORM_TRAINING);

    // Get table information for search context
    var tableInfoArr = GetAllTablesAndColumns(DB_DETAILS_LOCATION + "ready.json");
    Console.WriteLine($"Loaded {tableInfoArr.Count} tables from JSON file");

    // Initialize the search manager
    var searchManager = new DatabaseSearchManager(model, tableInfoArr);

    //// Demonstrate search functionality
    //await searchManager.DemonstrateSearchAsync();

    //// Perform diagnostic search for the specific query
    //await searchManager.PerformDiagnosticSearch(
    //    "Need to insert a new employee with firstname jon and lastname thomas in the PAY module",
    //    "dbo.tblPAY_EMPLOYEES"
    //);

    //// Perform enhanced search for the specific query
    //await searchManager.PerformEnhancedSearch(
    //    "Need to insert a new employee with firstname jon and lastname thomas in the PAY module",
    //    "dbo.tblPAY_EMPLOYEES"
    //);

    // Perform intelligent search with query-aware boosting (NEW!)
    await searchManager.PerformIntelligentSearch(
        "We need all of the columns for dbo.PAY_Employees"
    );

    //// Direct search for the specific table document
    //await searchManager.SearchForSpecificTableDocument("dbo.tblPAY_EMPLOYEES");

    //// Verify document count
    //await searchManager.VerifyDocumentCount();

    //// Diagnose vector store issues
    //await searchManager.DiagnoseVectorStoreIssues();

    // Optional: Search for specific table or column
    // await searchManager.SearchForSpecificTable("dbo.tblPAY_EMPLOYEES");
    // await searchManager.SearchForSpecificColumn("dbo.tblPAY_EMPLOYEES", "EmployeeID");

    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey();
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey();
}

// Helper method to load table information (moved from training manager for reuse)
static List<TableInfo> GetAllTablesAndColumns(string jsonFilePath)
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