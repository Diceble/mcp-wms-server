using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Wms.Domain.Queries;

namespace Wms.McpServer;

/// <summary>
/// The MCP contract. Claude only sees these names, descriptions, parameters and return shapes;
/// each tool is a thin wrapper over <see cref="IInventoryQueries"/>.
/// </summary>
[McpServerToolType]
public sealed class WmsTools(IInventoryQueries queries)
{
    [McpServerTool(Name = "search_products", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Find products by SKU fragment or product name (case-insensitive, at most 25 results). " +
        "Use this first when the user names a product instead of giving an exact SKU. " +
        "'available' is the shippable, unreserved quantity summed over all locations.")]
    public Task<IReadOnlyList<ProductSummary>> SearchProducts(
        [Description("Part of a SKU or product name, e.g. 'brooks' or 'CHN-'.")] string query,
        CancellationToken ct) =>
        queries.SearchProductsAsync(query, ct);

    [McpServerTool(Name = "get_stock_for_product", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Where a product is stored and how much per location (bin). Requires an exact SKU; use search_products first if you only have a name. " +
        "Per location: quantity is physically present, reserved is held for orders, available is quantity minus reserved and is 0 in non-shippable zones, " +
        "reservedFor lists which orders hold the reserved stock, with their promisedDate and daysUntilPromised: " +
        "a reservation for an order due much later could be moved to a more urgent one. " +
        "Zones Pick, Bulk and Recv (receiving) are shippable; Qc (quality control) and Dmg (damaged) are not. " +
        "Totals split non-shippable stock into inQualityControl and damaged. " +
        "reservationMismatch = true means the stored reserved total differs from the sum of reservedFor, a data quality issue worth mentioning.")]
    public async Task<ProductStock> GetStockForProduct(
        [Description("Exact SKU, e.g. 'CAS-SHI-HG50-1132'. Case-insensitive.")] string sku,
        CancellationToken ct) =>
        await queries.GetStockForProductAsync(sku, ct)
        ?? throw new McpException($"No product with SKU '{sku}'. Use search_products to find the SKU by name.");

    [McpServerTool(Name = "get_low_stock_products", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Products whose available stock is below their reorder level, largest shortfall first. " +
        "Available is on-hand stock minus reserved, excluding stock in quality control and damaged zones, " +
        "so a product can be low even when physical stock looks sufficient. " +
        "Use reserved, inQualityControl and damaged to explain why a product is low.")]
    public Task<IReadOnlyList<LowStockProduct>> GetLowStockProducts(CancellationToken ct) =>
        queries.GetLowStockProductsAsync(ct);

    [McpServerTool(Name = "get_order", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "An order with its status and lines. Status codes are resolved: status.meaning explains the code, " +
        "status.isKnown = false means an unrecognized code, status.isOpen = false only for shipped or cancelled orders. " +
        "daysUntilPromised is negative when the promised date has passed. " +
        "Per line: quantity ordered, quantityShipped, quantityOpen still to ship, quantityReserved already held for this order. " +
        "To check whether an order can ship in full: call get_stock_for_product for every line with quantityOpen > 0. " +
        "A line is covered when quantityOpen <= quantityReserved + available; the order's own reservations are part of 'reserved', not of 'available'. " +
        "Lines for the same SKU share its available stock. Orders on hold (HLD), closed or with an unknown status should not ship without a check.")]
    public async Task<OrderDetail> GetOrder(
        [Description("Order number, e.g. 1042.")] int orderNumber,
        CancellationToken ct) =>
        await queries.GetOrderAsync(orderNumber, ct)
        ?? throw new McpException($"No order with number {orderNumber}.");

    [McpServerTool(Name = "get_late_orders", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Open orders that are late (promised date has passed) or at risk (due within atRiskWithinDays days and with a problem: " +
        "insufficient stock, on hold, backorder or an unknown status code). Each order lists its reasons; " +
        "for late orders without a stock or status problem, the reasons give the current status as the clue to the delay. " +
        "daysUntilPromised is negative for late orders. Shipped and cancelled orders are never included. " +
        "Stock shortages are checked per order, without dividing stock between competing orders. " +
        "A shortage reason names the other open orders that need the same SKU, including orders outside the at-risk window, " +
        "so competing demand can be larger than this list suggests. Use get_order and get_stock_for_product for details.")]
    public Task<IReadOnlyList<LateOrder>> GetLateOrders(
        [Description("How many days ahead count as at risk. Default 2; 0 means only orders due today.")] int atRiskWithinDays = 2,
        CancellationToken ct = default) =>
        atRiskWithinDays < 0
            ? throw new McpException("atRiskWithinDays cannot be negative.")
            : queries.GetLateOrdersAsync(atRiskWithinDays, ct);
}

public static class WmsToolsRegistration
{
    /// <summary>camelCase with enums as strings, e.g. "zone": "Qc".</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Registers the MCP server with the WMS tools and instructions; the caller adds a transport.</summary>
    public static IMcpServerBuilder AddWmsMcpServer(this IServiceCollection services, DateOnly today) =>
        services
            .AddMcpServer(o => o.ServerInstructions = ServerInstructions.For(today))
            .WithTools<WmsTools>(JsonOptions);
}
