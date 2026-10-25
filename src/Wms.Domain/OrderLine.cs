namespace Wms.Domain;

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }
    public int QuantityShipped { get; set; }

    /// <summary>
    /// Quantity still to ship on this line. Independent of the order status: see
    /// <see cref="OrderStatusInfo.IsOpen"/> for why both need checking.
    /// </summary>
    public int QuantityOpen => Math.Max(0, Quantity - QuantityShipped);

    public List<Reservation> Reservations { get; set; } = [];
}
