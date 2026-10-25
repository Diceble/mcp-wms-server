namespace Wms.Domain;

public class StockLevel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public int Quantity { get; set; }

    /// <summary>
    /// Stored reserved total, as in most WMSes. Leading for free stock; it can drift
    /// from the sum of <see cref="Reservation"/> rows.
    /// </summary>
    public int Reserved { get; set; }
}
