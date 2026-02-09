# Testing Quick Start

## One Command to Rule Them All

```powershell
.\scripts\test.ps1
```

That's it! This:
1. ✅ Builds your code
2. ✅ Launches Rhino + Grasshopper
3. ✅ Loads your plugin (no file copying!)
4. ✅ Opens the test file: `gh_scripts/gh_mcp_load.gh`

## Common Options

```powershell
# Keep Rhino open to inspect results
.\scripts\test.ps1 -KeepOpen

# Already built? Skip the build
.\scripts\test.ps1 -SkipBuild

# Headless testing with Grasshopper Player
.\scripts\test.ps1 -UsePlayer

# Debug build
.\scripts\test.ps1 -Configuration Debug -KeepOpen
```

## AI Workflow

```powershell
# Edit code → Test → Repeat
.\scripts\test.ps1
```

## Cleanup (One Time)

Old approach copied files. Remove them:
```powershell
Remove-Item "$env:APPDATA\Grasshopper\Libraries\GH_MCP.gha" -ErrorAction SilentlyContinue
```

## How It Works

Uses the **Visual Studio approach**:
- Sets `RHINO_PACKAGE_DIRS` environment variable
- Plugin loaded from: `GH_MCP\GH_MCP\bin\Release\net48\`
- **NO copying to Grasshopper\Libraries!**

## Full Details

See [TESTING.md](TESTING.md) for complete documentation.
