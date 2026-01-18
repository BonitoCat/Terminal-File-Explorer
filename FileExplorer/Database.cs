using Microsoft.Data.Sqlite;

namespace FileExplorer;

public static class Database
{
    private const string DbFile = "database.sqlite";
    private const string ConnectionString =
        "Data Source=database.sqlite;Foreign Keys=True;";

    public static void Initialize()
    {
        bool exists = File.Exists(DbFile);

        using SqliteConnection con = new(ConnectionString);
        con.Open();

        
        FileSystemWatcher watcher = new(DbFile);
        
        
        if (!exists)
        {
            using SqliteCommand cmd = con.CreateCommand();
            cmd.CommandText =
                """
                    CREATE TABLE Users(
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Path TEXT NOT NULL,
                    );
                """;
            cmd.ExecuteNonQuery();
        }
    }
}