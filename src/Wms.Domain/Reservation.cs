namespace Wms.Domain;

/// <summary>Stock held at a specific location for a specific order line.</summary>
public class Reservation
{
    public int Id { get; set; }
    public int OrderLineId { get; set; }
    public OrderLine OrderLine { get; set; } = null!;
    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public int Quantity { get; set; }
}
