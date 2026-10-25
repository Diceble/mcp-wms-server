namespace Wms.Domain;

public class Order
{
    public int Id { get; set; }
    public int OrderNumber { get; set; }
    public required string Customer { get; set; }

    /// <summary>Raw status code as stored; resolve it with <see cref="OrderStatus.Resolve"/>.</summary>
    public required string StatusCode { get; set; }

    public DateOnly PromisedDate { get; set; }

    public List<OrderLine> Lines { get; set; } = [];
}
