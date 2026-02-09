# Architecture

## High-level flow
Claude Desktop
  |
  v (MCP protocol)
Python Bridge Server (grasshopper_mcp/bridge.py)
  |
  v (TCP/JSON on port 8080)
Grasshopper Plugin (GH_MCP.gha)
  |
  v (UI thread)
Grasshopper Document

## Current implementation

| Layer | Technology | Key files |
| --- | --- | --- |
| MCP Server | Python + FastMCP | grasshopper_mcp/bridge.py |
| Communication | TCP/JSON | Port 8080, localhost |
| GH Plugin | C# (.NET 7/4.8) | GH_MCP/GH_MCP/*.cs |
| Output | .gha file | Deployed to %APPDATA%\Grasshopper\Libraries\ |

## Key components
- bridge.py (MCP server with tools and resources)
- GH_MCPComponent.cs (TCP server host within Grasshopper)
- ComponentCommandHandler.cs (component creation and management)
- ConnectionCommandHandler.cs (wire connection logic)
- FuzzyMatcher.cs (component name normalization)
- IntentRecognizer.cs (high-level pattern recognition)
