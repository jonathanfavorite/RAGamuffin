using RAGamuffin.Abstractions;

namespace RAGamuffin.Core;
public class SqliteDatabaseModel : IVectorDatabaseModel
{
    public string SqliteDbPath { get; set; }
    public string CollectionName { get; set; }
    public bool RetrainData { get; set; }

    public SqliteDatabaseModel(string sqliteDbPath, string collectionName, bool retrainData)
    {
        SqliteDbPath = sqliteDbPath;
        CollectionName = collectionName;
        RetrainData = retrainData;
    }
}
