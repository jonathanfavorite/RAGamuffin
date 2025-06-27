using Microsoft.Data.Sqlite;

namespace RAGamuffin.Common;

/// <summary>
/// Utility class for SQLite database operations.
/// Provides helper methods for creating and managing SQLite database files.
/// </summary>
public static class DbHelper
{
    /// <summary>
    /// Deletes a SQLite database file if it exists.
    /// </summary>
    /// <param name="path">Path to the SQLite database file</param>
    /// <exception cref="ArgumentException">Thrown when path is null or empty</exception>
    /// <exception cref="IOException">Thrown when the file cannot be deleted</exception>
    public static void DeleteSqliteDatabase(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Database path cannot be null or empty.", nameof(path));
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
        {
            throw new IOException($"Unable to delete database file at '{path}'. The file may be in use or you may not have sufficient permissions.", ex);
        }
    }

    /// <summary>
    /// Creates a new SQLite database file and initializes the connection.
    /// If the database already exists, this method will simply test the connection.
    /// </summary>
    /// <param name="path">Path to the SQLite database file</param>
    /// <exception cref="ArgumentException">Thrown when path is null or empty</exception>
    /// <exception cref="InvalidOperationException">Thrown when the database cannot be created or accessed</exception>
    public static void CreateSqliteDatabase(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Database path cannot be null or empty.", nameof(path));
        }

        try
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create connection string and test connection
            string connectionString = $"Data Source={path}";
            
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            
            // The database file will be created automatically when the connection is opened
        }
        catch (Exception ex) when (ex is SqliteException || ex is InvalidOperationException)
        {
            throw new InvalidOperationException($"Unable to create or access SQLite database at '{path}'. Please ensure you have write permissions to the directory.", ex);
        }
    }

    /// <summary>
    /// Checks if a SQLite database file exists and is accessible.
    /// </summary>
    /// <param name="path">Path to the SQLite database file</param>
    /// <returns>True if the database exists and can be accessed, false otherwise</returns>
    /// <exception cref="ArgumentException">Thrown when path is null or empty</exception>
    public static bool DatabaseExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Database path cannot be null or empty.", nameof(path));
        }

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            // Test if we can actually connect to the database
            string connectionString = $"Data Source={path}";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
