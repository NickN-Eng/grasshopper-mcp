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
- Automated testing scripts: See `testing/scripts/` for build, test, and deployment automation

## Autonomous Iteration Guidelines
- Prefer small, reversible changes; run the shortest relevant test subset first.
- Read saved log files to diagnose failures; do not rely on IDE output panes.
- If tests are missing for new behavior, add them before implementation.

## Runtime Log Locations
When GH_MCP.gha is loaded in Grasshopper, runtime logs are written to the GHA folder:
- `{GHA-folder}/script-commands.log` - All script component MCP command execution logs
- `{GHA-folder}/csharp-compilation.log` - C# script compilation attempt logs (Option A/B strategies)
- `{GHA-folder}/script-investigation-{timestamp}-{id}.txt` - Script component structure reports
- Example location: `GH_MCP/GH_MCP/bin/Debug/net48/script-commands.log`

These logs are essential for debugging script component operations and should be checked when commands fail.

## Diagnostic Components
The plugin includes diagnostic tools to investigate component structures:
- **Script Investigator** (`Params > Util`) - Connect any script component to investigate its structure via reflection
  - Use this to discover available properties/methods when implementing script component features
  - Generates detailed reports showing the actual component structure without assumptions
  - Essential for discovering how to interact with script components (get/set code, compile, etc.)
  - Report files saved to: `{GHA-folder}/script-investigation-{timestamp}-{id}.txt`

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

## Testing Documentation
- testing/TESTING.md - Main testing guide
- testing/AI-TESTING-QUICKSTART.md - Quick start for AI-driven testing
- testing/docs/ai-testing-guide.md - Comprehensive AI testing workflow
- testing/docs/testing-architecture.md - Testing system architecture
- testing/scripts/ - Automated test scripts

## Further Documentation
- docs/architecture.md
- docs/mcp.md
- docs/dev-quickstart.md
- docs/roadmap.md
