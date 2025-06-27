using Microsoft.Data.Sqlite;

namespace RAGamuffin.Core;
public static class DbHelper
{
    public static void DeleteSqliteDatabase(string path)
    {
       File.Delete(path);
    }
    public static void CreateSqliteDatabase(string path)
    {
        string connString = $"Data Source={path}";
        
        using var connection = new SqliteConnection(connString);
        connection.Open();
    }
}
