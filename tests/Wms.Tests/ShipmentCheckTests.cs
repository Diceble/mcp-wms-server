using static Wms.Infrastructure.Seeding.SeedScenarios;

namespace Wms.Tests;

public class ShipmentCheckTests(SeededDatabase database) : QueryTestBase(database)
{
    [Fact]
    public async Task Order_1042_cannot_ship_fully_because_brake_pads_are_short()
    {
        var check = await Queries.CheckShipmentAsync(ShortOrder, Ct);

        Assert.NotNull(check);
        Assert.False(check.CanShipFully);
        Assert.Empty(check.Blockers);

        var shortLine = Assert.Single(check.Lines, l => l.Shortfall > 0);
        Assert.Equal(BrakePadSku, shortLine.Sku);
        Assert.Equal((12, 0, 10, 2), (shortLine.QuantityOpen, shortLine.ReservedForThisOrder, shortLine.FreeAvailable, shortLine.Shortfall));
        Assert.Equal(
            "Short 2: 12 open, 0 reserved for this order, 10 free in shippable zones. " +
            "Not usable: 4 reserved for other orders, 4 in quality control, 0 damaged.",
            shortLine.Reason);
    }

    [Fact]
    public async Task Order_1042_other_lines_are_covered_by_own_reservations()
    {
        var check = await Queries.CheckShipmentAsync(ShortOrder, Ct);

        Assert.NotNull(check);
        var chain = Assert.Single(check.Lines, l => l.Sku == ChainSku);
        var tire = Assert.Single(check.Lines, l => l.Sku == TireSku);
        Assert.Equal((4, 4, 0), (chain.QuantityOpen, chain.ReservedForThisOrder, chain.Shortfall));
        Assert.Equal((4, 4, 0), (tire.QuantityOpen, tire.ReservedForThisOrder, tire.Shortfall));
    }

    [Fact]
    public async Task Own_reservation_makes_order_shippable()
    {
        var check = await Queries.CheckShipmentAsync(BrakePadReservationOrder, Ct);

        Assert.NotNull(check);
        Assert.True(check.CanShipFully);
    }

    [Theory]
    [InlineData(OnHoldDueTomorrow, "Order is on hold (HLD)")]
    [InlineData(ShippedInPast, "Order status is Shipped (SHP)")]
    [InlineData(CancelledInPast, "Order status is Cancelled (CNX)")]
    [InlineData(UnknownStatusDueTomorrow, "Unknown status code 'X9'; verify before shipping")]
    public async Task Status_blocks_shipping_even_with_stock(int orderNumber, string blocker)
    {
        var check = await Queries.CheckShipmentAsync(orderNumber, Ct);

        Assert.NotNull(check);
        Assert.False(check.CanShipFully);
        Assert.Contains(blocker, check.Blockers);
    }

    [Fact]
    public async Task Unknown_order_returns_null()
    {
        Assert.Null(await Queries.CheckShipmentAsync(999_999, Ct));
    }
}
