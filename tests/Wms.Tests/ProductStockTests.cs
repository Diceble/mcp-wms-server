using Wms.Domain;
using Wms.Domain.Queries;
using static Wms.Infrastructure.Seeding.SeedScenarios;

namespace Wms.Tests;

public class ProductStockTests(SeededDatabase database) : QueryTestBase(database)
{
    [Fact]
    public async Task Stock_is_listed_per_location_including_unshippable_zones()
    {
        var stock = await Queries.GetStockForProductAsync(CassetteSku, Ct);

        Assert.NotNull(stock);
        Assert.Equal(["A-03-02", MismatchBin, "DMG-01", "RECV-01"], stock.Locations.Select(l => l.BinCode));
        Assert.Equal(38, stock.OnHand);
        Assert.Equal((0, 3), (stock.InQualityControl, stock.Damaged));

        var damaged = Assert.Single(stock.Locations, l => l.Zone == Zone.Dmg);
        Assert.False(damaged.IsShippable);
        Assert.Equal(0, damaged.Available);
    }

    [Fact]
    public async Task Available_subtracts_stored_reserved_total()
    {
        var stock = await Queries.GetStockForProductAsync(CassetteSku, Ct);

        // A-03-02: 5, B-02-01: 20 - 3 reserved, RECV-01: 10, DMG-01: not shippable.
        Assert.NotNull(stock);
        Assert.Equal(32, stock.Available);
    }

    [Fact]
    public async Task Reservation_mismatch_is_flagged_only_where_it_exists()
    {
        var stock = await Queries.GetStockForProductAsync(CassetteSku, Ct);

        Assert.NotNull(stock);
        var mismatch = Assert.Single(stock.Locations, l => l.ReservationMismatch);
        Assert.Equal(MismatchBin, mismatch.BinCode);
        Assert.Equal(3, mismatch.Reserved);
        Assert.Equal([new LocationReservation(CassetteReservationOrder, 2, Database.Today.AddDays(12), 12)], mismatch.ReservedFor);
    }

    [Fact]
    public async Task Reserved_stock_names_the_orders_holding_it_and_when_they_are_due()
    {
        var stock = await Queries.GetStockForProductAsync(BrakePadSku, Ct);

        Assert.NotNull(stock);
        var pick = Assert.Single(stock.Locations, l => l.BinCode == "A-01-03");
        Assert.Equal([new LocationReservation(BrakePadReservationOrder, 4, Database.Today.AddDays(10), 10)], pick.ReservedFor);
        Assert.All(stock.Locations.Where(l => l.BinCode != "A-01-03"), l => Assert.Empty(l.ReservedFor));
    }

    [Fact]
    public async Task Sku_lookup_ignores_case_and_whitespace()
    {
        var stock = await Queries.GetStockForProductAsync("  cas-shi-hg50-1132 ", Ct);

        Assert.NotNull(stock);
        Assert.Equal(CassetteSku, stock.Sku);
    }

    [Fact]
    public async Task Unknown_sku_returns_null()
    {
        Assert.Null(await Queries.GetStockForProductAsync("DOES-NOT-EXIST", Ct));
    }

    [Fact]
    public async Task Search_matches_name_case_insensitively()
    {
        var results = await Queries.SearchProductsAsync("brooks", Ct);

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.StartsWith("SAD-BRK-B17", r.Sku));
    }

    [Fact]
    public async Task Search_matches_sku_fragment()
    {
        var results = await Queries.SearchProductsAsync("chn-", Ct);

        Assert.Equal([ChainSku, "CHN-SHI-HG601", "CHN-SRM-PC1110"], results.Select(r => r.Sku));
    }
}
