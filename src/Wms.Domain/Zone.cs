namespace Wms.Domain;

public enum Zone
{
    Pick,
    Bulk,
    Recv,
    Qc,
    Dmg,
}

public static class ZoneExtensions
{
    /// <summary>Stock in quality control or damaged zones cannot be shipped.</summary>
    public static bool IsShippable(this Zone zone) => zone is not (Zone.Qc or Zone.Dmg);
}
