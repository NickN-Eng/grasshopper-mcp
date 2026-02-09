# Dev Quickstart

## Build commands
```bash
# Python MCP Server
pip install -e .
grasshopper-mcp  # Starts the MCP bridge

# C# Plugin (Visual Studio or dotnet CLI)
cd GH_MCP
dotnet build -c Release

# Deploy plugin
copy GH_MCP\bin\Release\net48\GH_MCP.gha "%APPDATA%\Grasshopper\Libraries\"
```

## Start development session
```bash
# Terminal 1: Build and deploy plugin
cd GH_MCP && dotnet build && copy bin\Debug\net48\GH_MCP.gha "%APPDATA%\Grasshopper\Libraries\"

# Terminal 2: Start MCP server (current Python bridge)
grasshopper-mcp

# Rhino: Load Grasshopper, place GH_MCP component on canvas
```

## Testing strategy

### Automated testing (AI-driven)

**Quick start**:
```powershell
# Build and test (launches Rhino + Grasshopper with test file)
.\testing\scripts\test.ps1

# Keep Rhino open for inspection
.\testing\scripts\test.ps1 -KeepOpen

# Use Grasshopper Player (headless)
.\testing\scripts\test.ps1 -UsePlayer
```

**How it works**:
- ✅ Uses Visual Studio launch approach (RHINO_PACKAGE_DIRS)
- ✅ NO file copying to Grasshopper\Libraries
- ✅ Opens default test file: `gh_scripts/gh_mcp_load.gh`
- ✅ Plugin loaded directly from build directory

**What gets tested**:
- ✅ Plugin loading
- ✅ Component creation/deletion
- ✅ Component wiring
- ✅ MCP functionality in Grasshopper canvas

**For AI iteration**:
```powershell
# 1. Edit code
# 2. Run: .\testing\scripts\test.ps1
# 3. Inspect Grasshopper canvas
# 4. Iterate
```

### Manual testing
1. Load GH_MCP.gha in Grasshopper
2. Drop GH_MCP component on canvas
3. Run MCP server
4. Test via Claude Desktop

Or use the interactive console tester:
```powershell
# Build and run console tester
cd testing\testers\Console
dotnet run

# Commands: add, list, wire, search, params, etc.
# See testing\scripts\README.md for full command reference
```

## Resources
- Grasshopper SDK Documentation: https://developer.rhino3d.com/guides/grasshopper/
- Rhino 8 Scripting: https://developer.rhino3d.com/guides/scripting/
- Model Context Protocol: https://modelcontextprotocol.io/
- MCP C# SDK: https://github.com/modelcontextprotocol/csharp-sdk
