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
    int InQualityControl,
    int Damaged,
    IReadOnlyList<LocationStock> Locations);

/// <param name="ReservedFor">Which orders hold stock here, from the reservation rows.</param>
/// <param name="ReservationMismatch">The stored <paramref name="Reserved"/> total differs from the sum of <paramref name="ReservedFor"/>.</param>
public sealed record LocationStock(
    string BinCode,
    Zone Zone,
    bool IsShippable,
    int Quantity,
    int Reserved,
    int Available,
    IReadOnlyList<LocationReservation> ReservedFor,
    bool ReservationMismatch);

/// <summary>
/// Stock one order holds at a location. The promised date tells whether the reservation
/// could be moved to a more urgent order.
/// </summary>
/// <param name="DaysUntilPromised">Negative when the promised date has passed.</param>
public sealed record LocationReservation(int OrderNumber, int Quantity, DateOnly PromisedDate, int DaysUntilPromised);

public sealed record LowStockProduct(
    string Sku,
    string Name,
    int ReorderLevel,
    int Available,
    int OnHand,
    int Reserved,
    int InQualityControl,
    int Damaged)
{
    public int Shortfall => ReorderLevel - Available;
}

/// <param name="DaysUntilPromised">Negative when the promised date has passed.</param>
public sealed record OrderDetail(
    int OrderNumber,
    string Customer,
    OrderStatusInfo Status,
    DateOnly PromisedDate,
    int DaysUntilPromised,
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
