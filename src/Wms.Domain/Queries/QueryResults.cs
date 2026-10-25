namespace Wms.Domain.Queries;

// Available = quantity minus stored reserved, counted only in shippable zones.

public sealed record ProductSummary(string Sku, string Name, int Available);

public sealed record ProductStock(
    string Sku,
    string Name,
    int ReorderLevel,
    int OnHand,
    int Reserved,
    int Available,
    int Unshippable,
    IReadOnlyList<LocationStock> Locations);

public sealed record LocationStock(
    string BinCode,
    Zone Zone,
    bool IsShippable,
    int Quantity,
    int Reserved,
    int Available,
    int ReservedByOrders,
    bool ReservationMismatch);

public sealed record LowStockProduct(
    string Sku,
    string Name,
    int ReorderLevel,
    int Available,
    int OnHand,
    int Reserved,
    int Unshippable)
{
    public int Shortfall => ReorderLevel - Available;
}

public sealed record OrderDetail(
    int OrderNumber,
    string Customer,
    OrderStatusInfo Status,
    DateOnly PromisedDate,
    IReadOnlyList<OrderLineDetail> Lines);

public sealed record OrderLineDetail(
    string Sku,
    string ProductName,
    int Quantity,
    int QuantityShipped,
    int QuantityOpen,
    int QuantityReserved);

public enum LateOrderCategory
{
    Late,
    AtRisk,
}

/// <param name="DaysUntilPromised">Negative when the promised date has passed.</param>
public sealed record LateOrder(
    int OrderNumber,
    string Customer,
    OrderStatusInfo Status,
    DateOnly PromisedDate,
    LateOrderCategory Category,
    int DaysUntilPromised,
    IReadOnlyList<string> Reasons);

public sealed record ShipmentCheck(
    int OrderNumber,
    OrderStatusInfo Status,
    bool CanShipFully,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<ShipmentLineCheck> Lines);

public sealed record ShipmentLineCheck(
    string Sku,
    string ProductName,
    int QuantityOpen,
    int ReservedForThisOrder,
    int FreeAvailable,
    int Shortfall,
    string? Reason);
