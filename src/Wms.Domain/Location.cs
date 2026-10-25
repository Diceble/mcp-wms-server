namespace Wms.Domain;

public class Location
{
    public int Id { get; set; }
    public required string BinCode { get; set; }
    public Zone Zone { get; set; }
}
