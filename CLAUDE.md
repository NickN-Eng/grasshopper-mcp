# Grasshopper MCP - AI Project Instructions

## Purpose
- Grasshopper-MCP bridges Grasshopper (Rhino) and Claude Desktop via MCP to automate component creation, wiring, and parametric workflows.

## Repo Map
- grasshopper_mcp/bridge.py - Python MCP server (current bridge)
- GH_MCP/GH_MCP/ - C# Grasshopper plugin source
- GH_MCP/GH_MCP.sln - C# solution
- docs/ - architecture, roadmap, MCP tool list, dev quickstart

## AI-Driven TDD and Build Requirements
- For any testable change, write or update tests first.
- Run tests from the CLI and keep logs on disk so the AI can read them.
- Always run a .NET build for C# changes to catch compile-time errors.

### Standard commands (log to files)
- Create `logs/` at repo root if missing. Logs are local-only and must not be committed.
- Build (C#): `cd GH_MCP; dotnet build -c Release -bl:..\logs\dotnet-build.binlog`
- Tests (when a test project exists): `dotnet test -c Release --logger "trx;LogFileName=tests.trx" --results-directory ..\logs`
- Python tests (when tests exist): `python -m pytest`

## Autonomous Iteration Guidelines
- Prefer small, reversible changes; run the shortest relevant test subset first.
- Read saved log files to diagnose failures; do not rely on IDE output panes.
- If tests are missing for new behavior, add them before implementation.

## Grasshopper/Rhino Automation Notes
- For headless or automated checks, use Rhino.Compute / Hops to solve Grasshopper definitions via a compute server.
- Use compute outputs to validate expected values in tests or scripts.

## Code Style Guidelines

### C# Conventions
- Use async/await for I/O operations
- Execute Grasshopper operations on UI thread via `RhinoApp.InvokeOnUiThread`
- Use Newtonsoft.Json for serialization
- Target multiple frameworks: net48, net7.0-windows

### Python Conventions
- Use type hints throughout
- Async functions with async/await
- FastMCP decorators for tools/resources

## Commit Message Format
```
<type>: <description in present tense>

Types: feat, fix, refactor, docs, test, chore
Example: feat: Add script component code extraction
```

## Further Documentation
- docs/architecture.md
- docs/mcp.md
- docs/dev-quickstart.md
- docs/roadmap.md
