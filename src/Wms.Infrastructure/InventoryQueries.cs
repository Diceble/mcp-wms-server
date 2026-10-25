using Microsoft.EntityFrameworkCore;
using Wms.Domain;
using Wms.Domain.Queries;

namespace Wms.Infrastructure;

public sealed class InventoryQueries(WmsDbContext db, TimeProvider time) : IInventoryQueries
{
    private const int MaxSearchResults = 25;

    public async Task<IReadOnlyList<ProductSummary>> SearchProductsAsync(string query, CancellationToken ct = default)
    {
        // SQLite LIKE is case-insensitive for ASCII.
        var pattern = $"%{query.Trim()}%";
        var products = await ProductsWithStock()
            .Where(p => EF.Functions.Like(p.Sku, pattern) || EF.Functions.Like(p.Name, pattern))
            .OrderBy(p => p.Sku)
            .Take(MaxSearchResults)
            .ToListAsync(ct);

        return products.Select(p => new ProductSummary(p.Sku, p.Name, p.StockLevels.Sum(Available))).ToList();
    }

    public async Task<ProductStock?> GetStockForProductAsync(string sku, CancellationToken ct = default)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        var product = await ProductsWithStock().SingleOrDefaultAsync(p => p.Sku == normalized, ct);
        if (product is null)
        {
            return null;
        }

        var reservedByLocation = await db.Reservations
            .Where(r => r.OrderLine.ProductId == product.Id)
            .GroupBy(r => r.LocationId)
            .Select(g => new { LocationId = g.Key, Quantity = g.Sum(r => r.Quantity) })
            .ToDictionaryAsync(x => x.LocationId, x => x.Quantity, ct);

        var locations = product.StockLevels
            .OrderBy(s => s.Location.BinCode, StringComparer.Ordinal)
            .Select(s =>
            {
                var byOrders = reservedByLocation.GetValueOrDefault(s.LocationId);
                return new LocationStock(
                    s.Location.BinCode,
                    s.Location.Zone,
                    s.Location.Zone.IsShippable(),
                    s.Quantity,
                    s.Reserved,
                    Available(s),
                    byOrders,
                    ReservationMismatch: byOrders != s.Reserved);
            })
            .ToList();

        return new ProductStock(
            product.Sku,
            product.Name,
            product.ReorderLevel,
            OnHand: product.StockLevels.Sum(s => s.Quantity),
            Reserved: product.StockLevels.Sum(s => s.Reserved),
            Available: product.StockLevels.Sum(Available),
            Unshippable: product.StockLevels.Sum(Unshippable),
            locations);
    }

    public async Task<IReadOnlyList<LowStockProduct>> GetLowStockProductsAsync(CancellationToken ct = default)
    {
        var products = await ProductsWithStock().ToListAsync(ct);

        return products
            .Select(p => new LowStockProduct(
                p.Sku,
                p.Name,
                p.ReorderLevel,
                Available: p.StockLevels.Sum(Available),
                OnHand: p.StockLevels.Sum(s => s.Quantity),
                Reserved: p.StockLevels.Sum(s => s.Reserved),
                Unshippable: p.StockLevels.Sum(Unshippable)))
            .Where(p => p.Available < p.ReorderLevel)
            .OrderByDescending(p => p.Shortfall)
            .ThenBy(p => p.Sku, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<OrderDetail?> GetOrderAsync(int orderNumber, CancellationToken ct = default)
    {
        var order = await OrdersWithLines().SingleOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);
        if (order is null)
        {
            return null;
        }

        var lines = order.Lines
            .OrderBy(l => l.Id)
            .Select(l => new OrderLineDetail(
                l.Product.Sku,
                l.Product.Name,
                l.Quantity,
                l.QuantityShipped,
                l.QuantityOpen,
                l.Reservations.Sum(r => r.Quantity)))
            .ToList();

        return new OrderDetail(order.OrderNumber, order.Customer, OrderStatus.Resolve(order.StatusCode), order.PromisedDate, lines);
    }

    public async Task<IReadOnlyList<LateOrder>> GetLateOrdersAsync(int atRiskWithinDays = 2, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(atRiskWithinDays);

        var today = time.Today();
        var horizon = today.AddDays(atRiskWithinDays);
        var candidates = await OrdersWithLines().Where(o => o.PromisedDate <= horizon).ToListAsync(ct);

        // Status is normalized in C#, so it is filtered in memory after the date filter in SQL.
        // An order needs both an open status and an open quantity: shipped or cancelled orders were
        // not late, and a status can lag behind lines that are already fully shipped.
        var openOrders = candidates
            .Select(o => (Order: o, Status: OrderStatus.Resolve(o.StatusCode)))
            .Where(x => x.Status.IsOpen && x.Order.Lines.Any(l => l.QuantityOpen > 0))
            .ToList();

        var stock = await LoadStockAsync(openOrders.SelectMany(x => x.Order.Lines), ct);
        var result = new List<LateOrder>();

        foreach (var (order, status) in openOrders)
        {
            // Each order is checked on its own; competing orders do not share free stock here.
            var problems = new List<string>();
            if (StatusProblem(status) is { } statusProblem)
            {
                problems.Add(statusProblem);
            }

            problems.AddRange(CheckLines(order, stock)
                .Where(l => l.Shortfall > 0)
                .Select(l => $"Insufficient stock for {l.Sku}: {l.QuantityOpen} open, short {l.Shortfall}"));

            var days = order.PromisedDate.DayNumber - today.DayNumber;
            if (days < 0)
            {
                result.Add(ToLateOrder(order, status, LateOrderCategory.Late, days,
                    [$"Promised date passed {-days} day(s) ago", .. problems]));
            }
            else if (problems.Count > 0)
            {
                result.Add(ToLateOrder(order, status, LateOrderCategory.AtRisk, days, problems));
            }
        }

        return result.OrderBy(o => o.DaysUntilPromised).ThenBy(o => o.OrderNumber).ToList();
    }

    public async Task<ShipmentCheck?> CheckShipmentAsync(int orderNumber, CancellationToken ct = default)
    {
        var order = await OrdersWithLines().SingleOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);
        if (order is null)
        {
            return null;
        }

        var status = OrderStatus.Resolve(order.StatusCode);
        var stock = await LoadStockAsync(order.Lines, ct);
        var lines = CheckLines(order, stock);

        var blockers = new List<string>();
        if (!status.IsOpen)
        {
            blockers.Add($"Order status is {status.Meaning} ({status.Code})");
        }
        else if (status.Code == "HLD")
        {
            blockers.Add("Order is on hold (HLD)");
        }
        else if (!status.IsKnown)
        {
            blockers.Add($"Unknown status code '{status.Code}'; verify before shipping");
        }

        // Open status but nothing left on the lines: the status was never updated.
        if (status.IsOpen && lines.All(l => l.QuantityOpen == 0))
        {
            blockers.Add("Nothing left to ship");
        }

        var canShip = blockers.Count == 0 && lines.All(l => l.Shortfall == 0);
        return new ShipmentCheck(order.OrderNumber, status, canShip, blockers, lines);
    }

    private static List<ShipmentLineCheck> CheckLines(Order order, Dictionary<int, List<StockLevel>> stockByProduct)
    {
        // Free stock is pooled per product, so two lines for the same SKU cannot both claim it.
        var freeLeft = new Dictionary<int, int>();
        var result = new List<ShipmentLineCheck>();

        foreach (var line in order.Lines.OrderBy(l => l.Id))
        {
            var levels = stockByProduct.GetValueOrDefault(line.ProductId) ?? [];
            if (!freeLeft.TryGetValue(line.ProductId, out var free))
            {
                free = levels.Sum(Available);
            }

            var open = line.QuantityOpen;
            var own = line.Reservations.Where(r => r.Location.Zone.IsShippable()).Sum(r => r.Quantity);
            var fromOwn = Math.Min(own, open);
            var fromFree = Math.Min(free, open - fromOwn);
            var shortfall = open - fromOwn - fromFree;
            freeLeft[line.ProductId] = free - fromFree;

            string? reason = null;
            if (shortfall > 0)
            {
                var reservedShippable = levels.Where(s => s.Location.Zone.IsShippable()).Sum(s => s.Reserved);
                var reservedForOthers = Math.Max(0, reservedShippable - own);
                var unshippable = levels.Sum(Unshippable);
                reason = $"Short {shortfall}: {open} open, {own} reserved for this order, {free} free in shippable zones. " +
                         $"Not usable: {reservedForOthers} reserved for other orders, {unshippable} in QC/DMG zones.";
            }

            result.Add(new ShipmentLineCheck(line.Product.Sku, line.Product.Name, open, own, free, shortfall, reason));
        }

        return result;
    }

    private static string? StatusProblem(OrderStatusInfo status) => status switch
    {
        { IsKnown: false } => $"Unknown status code '{status.Code}'",
        { Code: "HLD" } => "Order is on hold (HLD)",
        { Code: "BO" } => "Order is on backorder (BO)",
        _ => null,
    };

    private static LateOrder ToLateOrder(Order order, OrderStatusInfo status, LateOrderCategory category, int days, List<string> reasons) =>
        new(order.OrderNumber, order.Customer, status, order.PromisedDate, category, days, reasons);

    private async Task<Dictionary<int, List<StockLevel>>> LoadStockAsync(IEnumerable<OrderLine> lines, CancellationToken ct)
    {
        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var stock = await db.StockLevels
            .AsNoTracking()
            .Include(s => s.Location)
            .Where(s => productIds.Contains(s.ProductId))
            .ToListAsync(ct);

        return stock.GroupBy(s => s.ProductId).ToDictionary(g => g.Key, g => g.ToList());
    }

    private IQueryable<Product> ProductsWithStock() =>
        db.Products.AsNoTracking().Include(p => p.StockLevels).ThenInclude(s => s.Location);

    private IQueryable<Order> OrdersWithLines() =>
        db.Orders.AsNoTracking()
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .Include(o => o.Lines).ThenInclude(l => l.Reservations).ThenInclude(r => r.Location)
            .AsSplitQuery();

    private static int Available(StockLevel stock) =>
        stock.Location.Zone.IsShippable() ? Math.Max(0, stock.Quantity - stock.Reserved) : 0;

    private static int Unshippable(StockLevel stock) =>
        stock.Location.Zone.IsShippable() ? 0 : stock.Quantity;
}
