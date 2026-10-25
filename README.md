# MCP WMS Server

A small read-only MCP server in .NET that lets Claude answer questions about stock and orders from a warehouse database. See [docs/slice-1.md](docs/slice-1.md) for scope and design decisions.

## Build and test

```bash
dotnet test
```

Tests run against an in-memory SQLite database seeded with a fixed clock, including an end-to-end test that talks to the MCP server over the protocol.

## Tools

| Tool | Answers |
|---|---|
| `search_products` | Find a SKU by name or SKU fragment |
| `get_stock_for_product` | Where a SKU is stored, per location |
| `get_low_stock_products` | Which products are below their reorder level |
| `get_order` | An order with status and lines |
| `get_late_orders` | Which orders are late or at risk, and why |

## Connect to Claude Desktop

1. Publish the server:

   ```bash
   dotnet publish src/Wms.McpServer -c Release -o artifacts/mcp
   ```

2. Add it to `%APPDATA%\Claude\claude_desktop_config.json`:

   ```json
   {
     "mcpServers": {
       "wms": {
         "command": "C:\\Projects\\MCP WMS Server\\artifacts\\mcp\\Wms.McpServer.exe"
       }
     }
   }
   ```

3. Fully quit Claude Desktop (also from the system tray) and start it again.

To update the server, quit Claude Desktop first: while it runs, the published exe is locked and `dotnet publish` fails.

## Database

On every start the server seeds a fresh SQLite database, because seed dates are relative to today. By default it lives in memory, one per process: Claude Desktop can start several server instances at once, and they would otherwise fight over one file. The seed is deterministic, so every instance holds the same data.

To inspect the data with a SQLite tool, set `WMS_DB_PATH` to a file path. That file is recreated on every start, so only run a single instance in that mode.

Logs go to stderr, which Claude Desktop writes to `%APPDATA%\Claude\logs\mcp-server-wms.log`.
