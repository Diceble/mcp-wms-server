namespace Wms.McpServer;

/// <summary>
/// Sent to the client at initialization. Holds what applies across tools; tool descriptions
/// repeat the essentials because not every client shows server instructions to the model.
/// </summary>
public static class ServerInstructions
{
    public static string For(DateOnly today) =>
        $"""
        Read-only access to a bike-parts warehouse. Today is {today:yyyy-MM-dd} ({today.DayOfWeek}); promised dates and daysUntilPromised are relative to it.

        Terms:
        - available: stock that can ship now, quantity minus reserved, only in zones Pick, Bulk and Recv. Stock in Qc (quality control) and Dmg (damaged) never ships.
        - reserved: stock held for orders. get_stock_for_product shows per location which orders hold it and when those are due (reservedFor).
        - Status codes are already resolved (status.meaning). Shipped (SHP) and cancelled (CNX) orders are closed.

        Can an order ship in full today? Call get_order, then get_stock_for_product for every line with quantityOpen > 0.
        A line is covered when quantityOpen <= its own quantityReserved + available. Lines for the same SKU share available stock.
        An order on hold, closed or with an unknown status should not ship without a check.
        When a line falls short, say how much and why: stock in quality control, damaged, or reserved for other orders (name them and when they are due).

        The data is messy on purpose: report reservation mismatches and unknown status codes instead of guessing around them.
        """;
}
