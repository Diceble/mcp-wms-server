namespace Wms.Domain;

public class Product
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }

    /// <summary>Reorder when available (shippable, unreserved) stock drops below this level.</summary>
    public int ReorderLevel { get; set; }

    public List<StockLevel> StockLevels { get; set; } = [];
}
