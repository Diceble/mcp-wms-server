namespace Wms.Domain.Queries;

/// <summary>
/// Read-only questions about stock and orders. This is the contract the MCP tools sit on;
/// another WMS backend only needs its own implementation.
/// </summary>
public interface IInventoryQueries
{
    /// <summary>Case-insensitive match on SKU or name.</summary>
    Task<IReadOnlyList<ProductSummary>> SearchProductsAsync(string query, CancellationToken ct = default);

    /// <summary>Stock per location for one SKU, or null if the SKU does not exist.</summary>
    Task<ProductStock?> GetStockForProductAsync(string sku, CancellationToken ct = default);

    /// <summary>Products whose available stock is below their reorder level, largest shortfall first.</summary>
    Task<IReadOnlyList<LowStockProduct>> GetLowStockProductsAsync(CancellationToken ct = default);

    /// <summary>Order with its lines, or null if the order does not exist.</summary>
    Task<OrderDetail?> GetOrderAsync(int orderNumber, CancellationToken ct = default);

    /// <summary>
    /// Open orders past their promised date, plus orders due within <paramref name="atRiskWithinDays"/>
    /// days that have a problem (stock shortage, on hold, backorder, unknown status).
    /// </summary>
    Task<IReadOnlyList<LateOrder>> GetLateOrdersAsync(int atRiskWithinDays = 2, CancellationToken ct = default);

    /// <summary>Whether every open line can ship today, or null if the order does not exist.</summary>
    Task<ShipmentCheck?> CheckShipmentAsync(int orderNumber, CancellationToken ct = default);
}
