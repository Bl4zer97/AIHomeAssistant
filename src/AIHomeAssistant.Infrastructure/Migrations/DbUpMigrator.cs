using DbUp;
using System.Reflection;

namespace AIHomeAssistant.Infrastructure.Migrations;

public static class DbUpMigrator
{
    public static void Migrate(string connectionString)
    {
        // Ensure the directory for the SQLite file exists (SQLite creates the file but not directories).
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
        var dbPath = builder.DataSource;
        if (!string.IsNullOrWhiteSpace(dbPath) && dbPath != ":memory:")
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }

        var upgrader = DeployChanges.To
            .SqliteDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
            throw new InvalidOperationException("DbUp migration failed.", result.Error);
    }
}
