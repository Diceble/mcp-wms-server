using Microsoft.EntityFrameworkCore;
using Wms.Domain;
using static Wms.Infrastructure.Seeding.SeedScenarios;

namespace Wms.Tests;

public class LowStockTests(SeededDatabase database) : QueryTestBase(database)
{
    [Fact]
    public async Task Plainly_low_product_is_included()
    {
        var low = await Queries.GetLowStockProductsAsync(Ct);

        var tube = Assert.Single(low, p => p.Sku == TubeSku);
        Assert.Equal(3, tube.Available);
        Assert.Equal(17, tube.Shortfall);
    }

    [Fact]
    public async Task Reservations_count_against_available_stock()
    {
        var low = await Queries.GetLowStockProductsAsync(Ct);

        var saddle = Assert.Single(low, p => p.Sku == SaddleSku);
        Assert.Equal(12, saddle.OnHand);
        Assert.Equal(5, saddle.Reserved);
        Assert.Equal(7, saddle.Available);
    }

    [Fact]
    public async Task Qc_stock_does_not_count_as_available()
    {
        var low = await Queries.GetLowStockProductsAsync(Ct);

        var grips = Assert.Single(low, p => p.Sku == GripSku);
        Assert.Equal(18, grips.OnHand);
        Assert.Equal((10, 0), (grips.InQualityControl, grips.Damaged));
        Assert.Equal(8, grips.Available);
    }

    [Theory]
    [InlineData(PedalSku)]
    [InlineData(ChainSku)]
    [InlineData(BottleCageSku)]
    public async Task Products_at_or_above_reorder_level_are_excluded(string sku)
    {
        var low = await Queries.GetLowStockProductsAsync(Ct);

        Assert.DoesNotContain(low, p => p.Sku == sku);
    }

    [Fact]
    public async Task Result_matches_independent_calculation_over_all_data()
    {
        var products = await Db.Products.Include(p => p.StockLevels).ThenInclude(s => s.Location).ToListAsync(Ct);
        var expected = products
            .Where(p => p.StockLevels
                .Where(s => s.Location.Zone is not (Zone.Qc or Zone.Dmg))
                .Sum(s => Math.Max(0, s.Quantity - s.Reserved)) < p.ReorderLevel)
            .Select(p => p.Sku)
            .Order(StringComparer.Ordinal);

        var low = await Queries.GetLowStockProductsAsync(Ct);

        Assert.Equal(expected, low.Select(p => p.Sku).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Sorted_by_largest_shortfall_first()
    {
        var low = await Queries.GetLowStockProductsAsync(Ct);

        Assert.Equal(low.OrderByDescending(p => p.Shortfall).Select(p => p.Shortfall), low.Select(p => p.Shortfall));
    }
}
