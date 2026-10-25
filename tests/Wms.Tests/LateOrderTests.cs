using Wms.Domain.Queries;
using static Wms.Infrastructure.Seeding.SeedScenarios;

namespace Wms.Tests;

public class LateOrderTests(SeededDatabase database) : QueryTestBase(database)
{
    [Fact]
    public async Task Returns_exactly_the_late_and_at_risk_scenarios()
    {
        var orders = await Queries.GetLateOrdersAsync(ct: Ct);

        Assert.Equal(
            [LateNew, LateLowercaseStatus, ShortOrder, OnHoldDueTomorrow, UnknownStatusDueTomorrow, ShortDueInTwoDays, BackorderDueInTwoDays],
            orders.Select(o => o.OrderNumber));
    }

    [Fact]
    public async Task Late_orders_report_days_overdue()
    {
        var orders = await Queries.GetLateOrdersAsync(ct: Ct);

        var late = Assert.Single(orders, o => o.OrderNumber == LateNew);
        Assert.Equal(LateOrderCategory.Late, late.Category);
        Assert.Equal(-5, late.DaysUntilPromised);
        Assert.Equal(
            ["Promised date passed 5 day(s) ago", "All open lines have enough stock; status is New (NW)"],
            late.Reasons);
    }

    [Fact]
    public async Task Late_orders_show_the_normalized_status_as_the_clue()
    {
        var orders = await Queries.GetLateOrdersAsync(ct: Ct);

        var late = Assert.Single(orders, o => o.OrderNumber == LateLowercaseStatus);
        Assert.Equal(
            ["Promised date passed 2 day(s) ago", "All open lines have enough stock; status is Picking (PCK)"],
            late.Reasons);
    }

    [Theory]
    [InlineData(ShippedInPast)]
    [InlineData(CancelledInPast)]
    [InlineData(HealthyDueTomorrow)]
    [InlineData(ShortDueInThreeDays)]
    public async Task Closed_healthy_or_out_of_window_orders_are_excluded(int orderNumber)
    {
        var orders = await Queries.GetLateOrdersAsync(ct: Ct);

        Assert.DoesNotContain(orders, o => o.OrderNumber == orderNumber);
    }

    [Theory]
    [InlineData(OnHoldDueTomorrow, "Order is on hold (HLD)")]
    [InlineData(BackorderDueInTwoDays, "Order is on backorder (BO)")]
    [InlineData(UnknownStatusDueTomorrow, "Unknown status code 'X9'")]
    [InlineData(ShortDueInTwoDays, "Insufficient stock for TUB-SCH-SV17: 10 open, short 7; also needed by other open orders: 1109 (10 open)")]
    [InlineData(ShortOrder, "Insufficient stock for BRK-SHI-B01S: 12 open, short 2; also needed by other open orders: 1101 (4 open)")]
    public async Task At_risk_orders_explain_why(int orderNumber, string reason)
    {
        var orders = await Queries.GetLateOrdersAsync(ct: Ct);

        var order = Assert.Single(orders, o => o.OrderNumber == orderNumber);
        Assert.Equal(LateOrderCategory.AtRisk, order.Category);
        Assert.Equal([reason], order.Reasons);
    }

    [Fact]
    public async Task Wider_window_picks_up_later_problems()
    {
        var orders = await Queries.GetLateOrdersAsync(atRiskWithinDays: 3, Ct);

        var order = Assert.Single(orders, o => o.OrderNumber == ShortDueInThreeDays);
        Assert.Equal(3, order.DaysUntilPromised);
        Assert.Equal(["Insufficient stock for TUB-SCH-SV17: 10 open, short 7; also needed by other open orders: 1108 (10 open)"], order.Reasons);
    }

    [Fact]
    public async Task Negative_window_is_rejected()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Queries.GetLateOrdersAsync(-1, Ct));
    }
}
