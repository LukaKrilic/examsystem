using System.Text.RegularExpressions;
using ExamSystem.Web.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Tests;

// Builds a REAL throwaway SQL Server database from database/Database.sql (never SQLite/InMemory — we'd
// lose the filtered unique index and real T-SQL semantics), then drops it on dispose. Shared across the
// test collection so the whole suite reuses one DB. This doubles as the entity↔schema drift alarm:
// if an entity stops matching the script, the first query against it throws.
public sealed class ExamSystemTestFixture : IAsyncLifetime
{
    // Same server the dev app uses (appsettings.Development.json). LocalDB works too — change here if needed.
    private const string Server = "localhost";
    private const string TestDb = "ExamSystem_Test";

    private static string MasterConnectionString =>
        $"Server={Server};Database=master;Trusted_Connection=True;TrustServerCertificate=True";

    public string ConnectionString =>
        $"Server={Server};Database={TestDb};Trusted_Connection=True;TrustServerCertificate=True";

    public async Task InitializeAsync()
    {
        await DropTestDatabaseAsync();

        // Take database/Database.sql verbatim, retarget it at ExamSystem_Test, split on GO batch
        // separators (GO is an SSMS directive, not T-SQL — SqlCommand can't execute it), run each batch.
        var script = await File.ReadAllTextAsync(LocateDatabaseScript());
        script = script.Replace("ExamSystem", TestDb);

        await using var conn = new SqlConnection(MasterConnectionString);
        await conn.OpenAsync();
        foreach (var batch in Regex.Split(script, @"(?im)^\s*GO\s*$"))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            await using var cmd = new SqlCommand(batch, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DisposeAsync() => await DropTestDatabaseAsync();

    // Fresh DbContext pointed at the throwaway DB; each test owns its own instance.
    public ExamDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ExamDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new ExamDbContext(options);
    }

    private static async Task DropTestDatabaseAsync()
    {
        const string sql = $"""
            IF DB_ID(N'{TestDb}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{TestDb}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{TestDb}];
            END
            """;
        await using var conn = new SqlConnection(MasterConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    // Walk up from the test assembly until we find the repo's database/Database.sql.
    private static string LocateDatabaseScript()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "database", "Database.sql");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate database/Database.sql above the test assembly.");
    }
}

[CollectionDefinition(Name)]
public sealed class ExamSystemTestCollection : ICollectionFixture<ExamSystemTestFixture>
{
    public const string Name = "ExamSystem_Test";
}
