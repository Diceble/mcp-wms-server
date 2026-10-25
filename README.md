# MCP WMS Server

A small, read-only [Model Context Protocol](https://modelcontextprotocol.io) server in .NET that lets Claude answer questions about stock and orders straight from a warehouse database.

> **Demo:** _link to the 2–3 minute video_

You ask Claude in plain language. Claude picks the tools, combines them, and explains the answer, including the messy parts of the data. A typical answer, condensed:

> **Can we ship order 1042 in full today?**
>
> No. The chain and the tires are covered by the order's own reservations, but the brake pads are 2 short: 12 open, 10 free. Four more sit in quality control and four on A-01-03 are reserved for order 1101, which is only due in 10 days. Moving that reservation, or releasing the QC batch, would let 1042 ship today.

## The four questions

The project is done when these are answered correctly from the database:

| # | Question | Tools Claude uses |
|---|---|---|
| 1 | Which products are below their reorder level? | `get_low_stock_products` |
| 2 | Where is SKU X stored, and how much per location? | `search_products` → `get_stock_for_product` |
| 3 | Which orders are late or at risk, and why? | `get_late_orders` |
| 4 | Can we ship order 1042 in full today? | `get_order` → `get_stock_for_product` per line |

Question 4 has no dedicated tool on purpose: Claude has to combine tools, and that is where MCP adds real value.

## Tools

All tools are read-only and return camelCase JSON.

| Tool | Parameters | Returns |
|---|---|---|
| `search_products` | `query` | Matching SKUs with available stock |
| `get_stock_for_product` | `sku` | Stock per location: quantity, reserved, available, zone, which orders hold the reservations and when they are due |
| `get_low_stock_products` | none | Products below their reorder level, with reserved, quality-control and damaged stock to explain why |
| `get_order` | `orderNumber` | Status (code and meaning) and lines with open and reserved quantities |
| `get_late_orders` | `atRiskWithinDays` (default 2) | Late and at-risk orders with reasons, including other orders competing for the same stock |

The server also sends **instructions** at startup: today's date, the key terms, and the recipe for checking whether an order can ship.

## Architecture

```
src/
  Wms.Domain/          entities, status codes, IInventoryQueries and its result types
  Wms.Infrastructure/  EF Core + SQLite, deterministic seed, IInventoryQueries implementation
  Wms.McpServer/       MCP host (stdio), tools, server instructions
tests/
  Wms.Tests/           query tests on seeded data, end-to-end tests over the MCP protocol
```

```
Claude Desktop ──stdio/JSON-RPC──▶ Wms.McpServer ──▶ IInventoryQueries ──▶ EF Core ──▶ SQLite
                                   (tools = contract)  (swappable backend)
```

Built with .NET 10, EF Core 10 with SQLite, and the official [`ModelContextProtocol`](https://www.nuget.org/packages/ModelContextProtocol) C# SDK.

## Design decisions

- **Narrow tools, no `run_sql`.** Five purpose-built tools are safer than generic SQL access, and they let the server own the business rules (what "available" means, which zones ship).
- **The tools are the contract, not the tables.** Claude only sees tool names, descriptions, parameters and return shapes. Connecting another WMS means writing a second `IInventoryQueries` implementation; the tools stay the same.
- **Deliberately messy seed data.** Clean data hides the hard part. The seed contains late orders, low stock, stock spread over locations, stock in quality control and damaged zones, cryptic and unknown status codes (`pck`, `X9`), partially shipped lines, reserved-but-not-picked stock, and one stored reservation total that does not match its reservation rows.
- **Reservations belong to order lines.** A `Reservation` table links stock at a location to an order line, so "can this order ship?" counts the order's own reservations and not those of other orders.
- **Unknown status codes count as open.** Treating them as closed would silently drop orders from the late-order check. A false alarm is cheaper than a missed order.
- **One in-memory database per server process.** Seed dates are relative to today, so the database is rebuilt on every start. Claude Desktop can start several server instances at once, so each gets its own database instead of fighting over one file.
- **Logs go to stderr.** stdout carries the MCP protocol; a single stray line there breaks the connection.

## Getting started

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Claude Desktop](https://claude.ai/download).

```bash
dotnet test
```

```bash
dotnet publish src/Wms.McpServer -c Release -o artifacts/mcp
```

Add the server to `%APPDATA%\Claude\claude_desktop_config.json` (on macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`), using the full path to the published executable:

```json
{
  "mcpServers": {
    "wms": {
      "command": "C:\\path\\to\\repo\\artifacts\\mcp\\Wms.McpServer.exe"
    }
  }
}
```

Fully quit Claude Desktop (also from the system tray), start it again, open a new chat and ask one of the four questions. While Claude Desktop runs, the published executable is locked; quit it before publishing again.

To inspect the data with a SQLite tool, set the environment variable `WMS_DB_PATH` to a file path. That file is recreated on every start, so run only one instance in that mode. Server logs end up in `%APPDATA%\Claude\logs\mcp-server-wms.log`.

## Testing

70 tests on xUnit v3:

- **Query layer** against an in-memory SQLite database seeded with a fixed clock. Hand-crafted scenario rows have named constants in [`SeedScenarios`](src/Wms.Infrastructure/Seeding/SeedScenarios.cs); property-style tests check the results against an independent calculation over all data.
- **End to end** over the MCP protocol: the real server runs in-process, connected to an MCP client through in-memory pipes. One test applies the shipping recipe from the tool descriptions to tool output only, and checks it finds the same shortage as the query layer.
- **Regression** for concurrent server instances and parallel tool calls.

The answers Claude gave in Claude Desktop were checked against an [evaluation set](docs/week-3-evaluatie.md) (Dutch).

## What I learned about tool descriptions

- **Give the model the data, not just an instruction.** Asking Claude to mention when a reserving order is due made it guess, until the reservation itself carried the promised date.
- **What a tool filters out does not exist for the model.** With an at-risk window of two days, Claude concluded no orders competed for stock. Shortage reasons now name competing orders outside the window too.
- **Split fields that call for different actions.** One `unshippable` number mixed stock waiting for quality control (approve it) with damaged stock (write it off).
- **Server instructions reach the model** in Claude Desktop, which makes them the place for shared terms and recipes that apply across tools.

## Out of scope

Parked for later slices: write actions (reserving stock, creating orders), an inventory ledger of stock movements, HTTP transport and authentication, a web UI, multiple warehouses, and Shopify or EDI integrations. See [docs/slice-1.md](docs/slice-1.md) (Dutch) for the full scope and decisions.

## License

[MIT](LICENSE)
