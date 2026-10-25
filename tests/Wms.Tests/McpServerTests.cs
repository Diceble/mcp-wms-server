using System.IO.Pipelines;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Wms.Infrastructure;
using Wms.McpServer;
using static Wms.Infrastructure.Seeding.SeedScenarios;

namespace Wms.Tests;

/// <summary>Runs the real MCP server in-process, connected to a client over in-memory pipes.</summary>
public sealed class McpServerFixture : IAsyncLifetime
{
    private readonly SeededDatabase _database = new();
    private IHost? _host;

    public McpClient Client { get; private set; } = null!;

    private bool Started => Client is not null;

    public async ValueTask InitializeAsync()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<TimeProvider>(_database.Time);
        builder.Services.AddWmsInventory(o => o.UseSqlite(_database.Connection));
        builder.Services
            .AddMcpServer()
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
            .WithWmsTools();

        _host = builder.Build();
        await _host.StartAsync();

        Client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()));
    }

    public async ValueTask DisposeAsync()
    {
        if (Started)
        {
            await Client.DisposeAsync();
        }

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        _database.Dispose();
    }
}

public class McpServerTests(McpServerFixture server) : IClassFixture<McpServerFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Exposes_exactly_the_five_read_only_tools()
    {
        var tools = await server.Client.ListToolsAsync(cancellationToken: Ct);

        Assert.Equal(
            ["get_late_orders", "get_low_stock_products", "get_order", "get_stock_for_product", "search_products"],
            tools.Select(t => t.Name).Order(StringComparer.Ordinal));
        Assert.All(tools, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Description));
            Assert.True(t.ProtocolTool.Annotations?.ReadOnlyHint);
        });
    }

    [Theory]
    [InlineData("search_products", "query")]
    [InlineData("get_stock_for_product", "sku")]
    [InlineData("get_order", "orderNumber")]
    public async Task Tools_require_their_parameter(string toolName, string parameter)
    {
        var tools = await server.Client.ListToolsAsync(cancellationToken: Ct);

        var schema = tools.Single(t => t.Name == toolName).JsonSchema;
        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString());
        Assert.Contains(parameter, required);
        Assert.False(string.IsNullOrWhiteSpace(schema.GetProperty("properties").GetProperty(parameter).GetProperty("description").GetString()));
    }

    [Fact]
    public async Task Low_stock_tool_returns_scenario_products()
    {
        var result = await CallAsync("get_low_stock_products");

        var skus = result.EnumerateArray().Select(p => p.GetProperty("sku").GetString()).ToList();
        Assert.Contains(TubeSku, skus);
        Assert.Contains(SaddleSku, skus);
        Assert.Contains(GripSku, skus);
        Assert.DoesNotContain(PedalSku, skus);
    }

    [Fact]
    public async Task Stock_tool_returns_camel_case_json_with_zone_names()
    {
        var result = await CallAsync("get_stock_for_product", new() { ["sku"] = CassetteSku });

        Assert.Equal(32, result.GetProperty("available").GetInt32());
        var locations = result.GetProperty("locations").EnumerateArray().ToList();
        Assert.Equal(4, locations.Count);
        Assert.Contains(locations, l => l.GetProperty("zone").GetString() == "Dmg" && !l.GetProperty("isShippable").GetBoolean());
        Assert.Single(locations, l => l.GetProperty("reservationMismatch").GetBoolean());
    }

    [Fact]
    public async Task Search_then_stock_finds_products_by_name()
    {
        var found = await CallAsync("search_products", new() { ["query"] = "brooks" });
        var sku = found.EnumerateArray().First().GetProperty("sku").GetString();

        var stock = await CallAsync("get_stock_for_product", new() { ["sku"] = sku });

        Assert.Equal(sku, stock.GetProperty("sku").GetString());
    }

    [Fact]
    public async Task Order_tool_resolves_status()
    {
        var result = await CallAsync("get_order", new() { ["orderNumber"] = ShortOrder });

        var status = result.GetProperty("status");
        Assert.Equal("PSH", status.GetProperty("code").GetString());
        Assert.Equal("Partially shipped", status.GetProperty("meaning").GetString());
        Assert.Equal(3, result.GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public async Task Late_orders_tool_uses_default_window()
    {
        var result = await CallAsync("get_late_orders");

        Assert.Equal(7, result.GetArrayLength());
        Assert.Contains(result.EnumerateArray(), o => o.GetProperty("category").GetString() == "Late");
    }

    [Theory]
    [InlineData("get_stock_for_product", "sku", "NOPE-123", "search_products")]
    [InlineData("get_order", "orderNumber", 999999, "No order with number 999999")]
    [InlineData("get_late_orders", "atRiskWithinDays", -1, "cannot be negative")]
    public async Task Errors_are_reported_to_the_model(string toolName, string parameter, object value, string expectedText)
    {
        var result = await server.Client.CallToolAsync(toolName, new Dictionary<string, object?> { [parameter] = value }, cancellationToken: Ct);

        Assert.True(result.IsError);
        Assert.Contains(expectedText, Text(result));
    }

    private async Task<JsonElement> CallAsync(string toolName, Dictionary<string, object?>? arguments = null)
    {
        var result = await server.Client.CallToolAsync(toolName, arguments, cancellationToken: Ct);

        Assert.NotEqual(true, result.IsError);
        return JsonDocument.Parse(Text(result)).RootElement;
    }

    private static string Text(CallToolResult result) => Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
