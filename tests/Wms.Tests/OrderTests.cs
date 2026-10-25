using static Wms.Infrastructure.Seeding.SeedScenarios;

namespace Wms.Tests;

public class OrderTests(SeededDatabase database) : QueryTestBase(database)
{
    [Fact]
    public async Task Order_includes_lines_with_open_and_reserved_quantities()
    {
        var order = await Queries.GetOrderAsync(ShortOrder, Ct);

        Assert.NotNull(order);
        Assert.Equal("PSH", order.Status.Code);
        Assert.Equal("Partially shipped", order.Status.Meaning);
        Assert.Equal(Database.Today, order.PromisedDate);
        Assert.Equal(0, order.DaysUntilPromised);
        Assert.Equal([ChainSku, TireSku, BrakePadSku], order.Lines.Select(l => l.Sku));

        var tire = order.Lines[1];
        Assert.Equal((10, 6, 4, 4), (tire.Quantity, tire.QuantityShipped, tire.QuantityOpen, tire.QuantityReserved));
    }

    [Fact]
    public async Task Raw_status_code_is_resolved()
    {
        var order = await Queries.GetOrderAsync(LateLowercaseStatus, Ct);

        Assert.NotNull(order);
        Assert.Equal("PCK", order.Status.Code);
        Assert.True(order.Status.IsKnown);
        Assert.Equal(-2, order.DaysUntilPromised);
    }

    [Fact]
    public async Task Unknown_order_returns_null()
    {
        Assert.Null(await Queries.GetOrderAsync(999_999, Ct));
    }
}
