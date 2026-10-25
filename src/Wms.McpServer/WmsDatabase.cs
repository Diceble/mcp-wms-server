using Microsoft.Data.Sqlite;

namespace Wms.McpServer;

/// <summary>
/// Where the server keeps its data. Claude Desktop can start several server processes at once,
/// so by default each process gets its own in-memory database: recreating a shared file at
/// startup fails while another instance has it open. The seed is deterministic, so every
/// instance holds the same data.
/// </summary>
public sealed class WmsDatabase : IDisposable
{
    public const string PathVariable = "WMS_DB_PATH";

    // An in-memory database lives as long as at least one connection to it is open.
    private readonly SqliteConnection? _keepAlive;

    private WmsDatabase(string connectionString, string description, SqliteConnection? keepAlive)
    {
        ConnectionString = connectionString;
        Description = description;
        _keepAlive = keepAlive;
    }

    public string ConnectionString { get; }

    public string Description { get; }

    /// <summary>A file at <c>WMS_DB_PATH</c> if set (single instance only, for inspecting the data), otherwise in memory.</summary>
    public static WmsDatabase FromEnvironment()
    {
        var path = Environment.GetEnvironmentVariable(PathVariable);
        return string.IsNullOrWhiteSpace(path) ? InMemory() : File(path);
    }

    public static WmsDatabase InMemory()
    {
        // Shared cache lets every DbContext open its own connection to the same database,
        // so parallel tool calls never share one connection.
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"wms-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        var keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open();
        return new WmsDatabase(connectionString, "in-memory database", keepAlive);
    }

    public static WmsDatabase File(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        return new WmsDatabase(new SqliteConnectionStringBuilder { DataSource = fullPath }.ToString(), $"database file {fullPath}", null);
    }

    public void Dispose() => _keepAlive?.Dispose();
}
