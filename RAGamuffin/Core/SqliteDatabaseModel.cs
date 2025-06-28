using RAGamuffin.Abstractions;
using RAGamuffin.VectorStores;

namespace RAGamuffin.Core;
public class SqliteDatabaseModel : IVectorDatabaseModel
{
    public string SqliteDbPath { get; set; }
    public string CollectionName { get; set; }

    public SqliteDatabaseModel(string sqliteDbPath, string collectionName)
    {
        SqliteDbPath = sqliteDbPath;
        CollectionName = collectionName;
    }
}
