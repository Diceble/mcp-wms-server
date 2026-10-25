namespace Wms.Infrastructure.Seeding;

/// <summary>
/// Hand-crafted rows with fixed identifiers. Tests and the demo rely on these;
/// bulk data never touches these products and never lands in the at-risk window.
/// </summary>
public static class SeedScenarios
{
    /// <summary>Looks shippable on totals, but the brake pad line is 2 short (QC stock and another order's reservation).</summary>
    public const int ShortOrder = 1042;

    public const string ChainSku = "CHN-KMC-X11";
    public const string TireSku = "TIR-CON-GP5K-28";
    public const string BrakePadSku = "BRK-SHI-B01S";

    /// <summary>Physical stock is above the reorder level; only reservations push it below.</summary>
    public const string SaddleSku = "SAD-SEL-FLITE";

    /// <summary>Below the reorder level; QC stock would cover it if it were shippable.</summary>
    public const string GripSku = "GRP-ERG-GC1";

    /// <summary>Available stock exactly equals the reorder level, so it is not low.</summary>
    public const string PedalSku = "PDL-SHI-M520";

    /// <summary>Plainly low stock, also the cause of shortages on late orders.</summary>
    public const string TubeSku = "TUB-SCH-SV17";

    /// <summary>Spread over four locations, one of them damaged.</summary>
    public const string CassetteSku = "CAS-SHI-HG50-1132";

    /// <summary>Plenty of stock; used on orders whose only problem is the date or status.</summary>
    public const string BottleCageSku = "BTL-ELI-CAGE";

    /// <summary>The one stock level whose stored reserved total differs from its reservation rows.</summary>
    public const string MismatchBin = "B-02-01";

    public const int BrakePadReservationOrder = 1101;
    public const int SaddleReservationOrder = 1102;
    public const int CassetteReservationOrder = 1103;

    public const int LateNew = 1104;
    public const int ShippedInPast = 1105;
    public const int OnHoldDueTomorrow = 1106;
    public const int LateLowercaseStatus = 1107;
    public const int ShortDueInTwoDays = 1108;
    public const int ShortDueInThreeDays = 1109;
    public const int UnknownStatusDueTomorrow = 1110;
    public const int CancelledInPast = 1111;
    public const int BackorderDueInTwoDays = 1112;
    public const int HealthyDueTomorrow = 1113;
}
