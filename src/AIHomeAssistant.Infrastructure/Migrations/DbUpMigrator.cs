using DbUp;
using System.Reflection;

namespace AIHomeAssistant.Infrastructure.Migrations;

public static class DbUpMigrator
{
    public static void Migrate(string connectionString)
    {
        // SQLite creates the file automatically on first connection — no EnsureDatabase needed.
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
