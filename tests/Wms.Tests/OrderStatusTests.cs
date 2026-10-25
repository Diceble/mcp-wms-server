using Wms.Domain;

namespace Wms.Tests;

public class OrderStatusTests
{
    [Theory]
    [InlineData("SHP", "SHP", false)]
    [InlineData("CNX", "CNX", false)]
    [InlineData("ALC", "ALC", true)]
    [InlineData("pck", "PCK", true)]
    [InlineData(" PCK", "PCK", true)]
    [InlineData("Nw", "NW", true)]
    public void Known_codes_are_normalized(string raw, string code, bool isOpen)
    {
        var status = OrderStatus.Resolve(raw);

        Assert.True(status.IsKnown);
        Assert.Equal(code, status.Code);
        Assert.Equal(isOpen, status.IsOpen);
    }

    [Theory]
    [InlineData("X9")]
    [InlineData("")]
    [InlineData(null)]
    public void Unknown_codes_are_flagged_and_treated_as_open(string? raw)
    {
        var status = OrderStatus.Resolve(raw);

        Assert.False(status.IsKnown);
        Assert.True(status.IsOpen);
        Assert.Equal("Unknown status code", status.Meaning);
    }
}
