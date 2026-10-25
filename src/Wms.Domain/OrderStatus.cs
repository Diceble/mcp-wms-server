namespace Wms.Domain;

/// <summary>An order status code resolved to its meaning.</summary>
/// <param name="Code">Normalized code (trimmed, upper case), e.g. <c>PCK</c> for a stored <c>" pck"</c>.</param>
/// <param name="Meaning">Human-readable meaning, so callers (and Claude) never have to guess what a code means.</param>
/// <param name="IsKnown">False for codes not in the mapping, so callers can say "unknown" instead of inventing a meaning.</param>
/// <param name="IsOpen">
/// True when, by status, the order still needs work: it is not shipped (<c>SHP</c>) and not cancelled (<c>CNX</c>).
/// On hold (<c>HLD</c>) and backorder (<c>BO</c>) are open: the order is paused, but the customer is still waiting
/// and the promised date still applies. This says nothing about quantities; a <c>PSH</c> order can have every line
/// fully shipped when nobody updated the status. Check <see cref="OrderLine.QuantityOpen"/> as well.
/// </param>
public sealed record OrderStatusInfo(string Code, string Meaning, bool IsKnown, bool IsOpen);

/// <summary>
/// Translates raw status codes. <see cref="Order.StatusCode"/> stays a plain string rather than an enum because
/// the source system owns the codes: stored values can be messy (<c>"pck"</c>) or unknown (<c>"X9"</c>), and an
/// enum conversion would fail on read. Translating in one place keeps code lists out of the query layer and tools.
/// </summary>
public static class OrderStatus
{
    private static readonly Dictionary<string, (string Meaning, bool IsOpen)> Known = new()
    {
        ["NW"] = ("New", true),
        ["ALC"] = ("Allocated (stock reserved)", true),
        ["PCK"] = ("Picking", true),
        ["PSH"] = ("Partially shipped", true),
        ["SHP"] = ("Shipped", false),
        ["HLD"] = ("On hold", true),
        ["BO"] = ("Backorder", true),
        ["CNX"] = ("Cancelled", false),
    };

    /// <summary>
    /// Normalizes case and whitespace; the stored value is left untouched. Unknown codes are treated as open:
    /// treating them as closed would silently drop orders from late-order checks, which nobody would notice.
    /// A false alarm is cheaper than a missed order.
    /// </summary>
    public static OrderStatusInfo Resolve(string? rawCode)
    {
        var code = (rawCode ?? "").Trim().ToUpperInvariant();
        return Known.TryGetValue(code, out var known)
            ? new OrderStatusInfo(code, known.Meaning, IsKnown: true, known.IsOpen)
            : new OrderStatusInfo(code, "Unknown status code", IsKnown: false, IsOpen: true);
    }
}
