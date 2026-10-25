using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.Domain.Queries;
using Wms.Infrastructure;
using Wms.McpServer;
using static Wms.Infrastructure.Seeding.SeedScenarios;

namespace Wms.Tests;

public class WmsDatabaseTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Concurrent_instances_each_get_their_own_seeded_database()
    {
        // Regression: Claude Desktop starts several server processes; recreating one shared file crashed the second.
        using var first = WmsDatabase.InMemory();
        using var second = WmsDatabase.InMemory();
        await using var firstServices = BuildServices(first);
        await using var secondServices = BuildServices(second);

        firstServices.RecreateWmsDatabase();
        secondServices.RecreateWmsDatabase();
        firstServices.RecreateWmsDatabase();

        Assert.NotEqual(first.ConnectionString, second.ConnectionString);
        Assert.Equal(await LowStockSkusAsync(firstServices), await LowStockSkusAsync(secondServices));
    }

    [Fact]
    public async Task Parallel_tool_calls_do_not_share_a_connection()
    {
        using var database = WmsDatabase.InMemory();
        await using var services = BuildServices(database);
        services.RecreateWmsDatabase();

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => LowStockSkusAsync(services)));

        Assert.All(results, skus => Assert.Contains(TubeSku, skus));
    }

    private static ServiceProvider BuildServices(WmsDatabase database) =>
        new ServiceCollection()
            .AddWmsInventory(o => o.UseSqlite(database.ConnectionString))
            .BuildServiceProvider();

    private static async Task<List<string>> LowStockSkusAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IInventoryQueries>();
        var low = await queries.GetLowStockProductsAsync(Ct);
        return [.. low.Select(p => p.Sku)];
    }
}
