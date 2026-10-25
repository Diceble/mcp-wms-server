using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wms.Infrastructure;
using Wms.McpServer;

var builder = Host.CreateApplicationBuilder(args);

// stdout carries the MCP protocol; anything else written there breaks the connection.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

using var database = WmsDatabase.FromEnvironment();
builder.Services.AddWmsInventory(o => o.UseSqlite(database.ConnectionString));
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithWmsTools();

var app = builder.Build();
app.Services.RecreateWmsDatabase();
app.Services.GetRequiredService<ILogger<Program>>().LogInformation("Seeded {Database}", database.Description);
await app.RunAsync();
