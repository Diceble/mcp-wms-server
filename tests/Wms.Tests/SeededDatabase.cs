using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Wms.Infrastructure;
using Wms.Infrastructure.Seeding;

namespace Wms.Tests;

/// <summary>In-memory SQLite database seeded once with a fixed clock.</summary>
public sealed class SeededDatabase : IDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;

    public SeededDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var db = CreateContext();
        db.Database.EnsureCreated();
        WmsSeeder.Seed(db, Time);
    }

    public FakeTimeProvider Time { get; } = new(Now);

    public DateOnly Today => DateOnly.FromDateTime(Now.Date);

    public SqliteConnection Connection => _connection;

    public WmsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<WmsDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}

/// <summary>Base for query tests: a fresh context per test over the shared seeded database.</summary>
public abstract class QueryTestBase(SeededDatabase database) : IClassFixture<SeededDatabase>, IDisposable
{
    protected SeededDatabase Database { get; } = database;
    protected WmsDbContext Db { get; } = database.CreateContext();
    protected InventoryQueries Queries => new(Db, Database.Time);
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        Db.Dispose();
        GC.SuppressFinalize(this);
    }
}
