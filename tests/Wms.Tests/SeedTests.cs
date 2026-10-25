using Microsoft.EntityFrameworkCore;
using Wms.Infrastructure;
using static Wms.Infrastructure.Seeding.SeedScenarios;

namespace Wms.Tests;

public class SeedTests(SeededDatabase database) : QueryTestBase(database)
{
    [Fact]
    public async Task Seed_is_deterministic()
    {
        using var other = new SeededDatabase();
        using var otherDb = other.CreateContext();

        Assert.Equal(await SnapshotAsync(Db), await SnapshotAsync(otherDb));
    }

    [Fact]
    public async Task Seed_has_a_few_hundred_rows()
    {
        var rows = await Db.Products.CountAsync(Ct)
                   + await Db.Locations.CountAsync(Ct)
                   + await Db.StockLevels.CountAsync(Ct)
                   + await Db.Orders.CountAsync(Ct)
                   + await Db.OrderLines.CountAsync(Ct)
                   + await Db.Reservations.CountAsync(Ct);

        Assert.InRange(rows, 300, 1000);
    }

    [Fact]
    public async Task Stored_reserved_totals_match_reservations_except_the_one_mismatch()
    {
        var reservations = await Db.Reservations
            .GroupBy(r => new { r.OrderLine.ProductId, r.LocationId })
            .Select(g => new { g.Key.ProductId, g.Key.LocationId, Quantity = g.Sum(r => r.Quantity) })
            .ToListAsync(Ct);
        var stock = await Db.StockLevels.Include(s => s.Product).Include(s => s.Location).ToListAsync(Ct);

        var mismatches = stock
            .Where(s => s.Reserved != reservations
                .Where(r => r.ProductId == s.ProductId && r.LocationId == s.LocationId)
                .Sum(r => r.Quantity))
            .ToList();

        var mismatch = Assert.Single(mismatches);
        Assert.Equal((CassetteSku, MismatchBin), (mismatch.Product.Sku, mismatch.Location.BinCode));
    }

    [Fact]
    public async Task Reservations_never_exceed_stock()
    {
        var overReserved = await Db.StockLevels.Where(s => s.Reserved > s.Quantity).CountAsync(Ct);

        Assert.Equal(0, overReserved);
    }

    private static async Task<List<string>> SnapshotAsync(WmsDbContext db)
    {
        var ct = TestContext.Current.CancellationToken;
        var stock = await db.StockLevels
            .Select(s => $"{s.Product.Sku}|{s.Product.ReorderLevel}|{s.Location.BinCode}|{s.Quantity}|{s.Reserved}")
            .ToListAsync(ct);
        var lines = await db.OrderLines
            .Select(l => $"{l.Order.OrderNumber}|{l.Order.Customer}|{l.Order.StatusCode}|{l.Order.PromisedDate}|{l.Product.Sku}|{l.Quantity}|{l.QuantityShipped}")
            .ToListAsync(ct);
        var reservations = await db.Reservations
            .Select(r => $"{r.OrderLine.Order.OrderNumber}|{r.OrderLine.Product.Sku}|{r.Location.BinCode}|{r.Quantity}")
            .ToListAsync(ct);

        return [.. stock.Concat(lines).Concat(reservations).Order(StringComparer.Ordinal)];
    }
}
