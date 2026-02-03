# Grasshopper MCP - AI Testing Guide

## Quick Start

```powershell
# Build and test with default test file
.\scripts\test.ps1

# That's it! Rhino launches, plugin loads, test file opens
```

## How It Works

Uses the **Visual Studio launch approach**:
1. ✅ Builds `GH_MCP.sln` → outputs to `GH_MCP\GH_MCP\bin\Release\net48\`
2. ✅ Sets `RHINO_PACKAGE_DIRS` environment variable to build directory
3. ✅ Launches Rhino with `/netfx /runscript`
4. ✅ Opens `gh_scripts/gh_mcp_load.gh` test file
5. ✅ **NO file copying!** Plugin loaded directly from build output

## Options

```powershell
# Keep Rhino open for inspection
.\scripts\test.ps1 -KeepOpen

# Use Grasshopper Player (headless)
.\scripts\test.ps1 -UsePlayer

# Skip build if already built
.\scripts\test.ps1 -SkipBuild

# Custom test file
.\scripts\test.ps1 -GrasshopperFile "path\to\test.gh"

# Debug build
.\scripts\test.ps1 -Configuration Debug -KeepOpen
```

## AI Workflow

```powershell
# 1. Make code changes
# (AI edits .cs files)

# 2. Test
.\scripts\test.ps1

# 3. Check Grasshopper canvas
# Inspect components, connections, behavior

# 4. Iterate
# Make more changes, run again
```

## Default Test File

**`gh_scripts/gh_mcp_load.gh`**

This Grasshopper definition should contain:
- Components to test MCP functionality
- Expected behavior/output
- Visual verification points

## Cleanup Old Approach

If you previously copied the .gha to Grasshopper\Libraries:

```powershell
# Remove old copied file
Remove-Item "$env:APPDATA\Grasshopper\Libraries\GH_MCP.gha" -ErrorAction SilentlyContinue
```

## Visual Studio Equivalent

The test script does exactly what Visual Studio F5 does:

**launchSettings.json**:
```json
{
  "profiles": {
    "Rhino 8 - netfx": {
      "commandName": "Executable",
      "executablePath": "C:\\Program Files\\Rhino 8\\System\\Rhino.exe",
      "commandLineArgs": "/netfx /runscript=\"_Grasshopper\"",
      "environmentVariables": {
        "RHINO_PACKAGE_DIRS": "$(ProjectDir)$(OutputPath)\\"
      }
    }
  }
}
```

**Our script** = Same thing + opens test file automatically

## For Developers

### Set up Grasshopper developer settings (optional)

In Grasshopper:
1. File → Special Folders → Components Folder
2. Note the location
3. You can manually add your build output to this list

But **you don't need to** - the script handles it via environment variable.

### Advanced: GrasshopperPlayer

For truly headless testing:
```powershell
.\scripts\test.ps1 -UsePlayer
```

This runs the Grasshopper definition without showing the UI.

## Troubleshooting

### "Plugin not found"
```powershell
# Build without skipping
.\scripts\test.ps1 -SkipBuild:$false
```

### "Grasshopper file not found"
```powershell
# Check if it exists
ls gh_scripts\gh_mcp_load.gh

# Create if missing or specify different file
.\scripts\test.ps1 -GrasshopperFile "your\test.gh"
```

### "Rhino not found"
Edit `scripts\test.ps1` line 77:
```powershell
$RhinoExe = "C:\Program Files\Rhino 8\System\Rhino.exe"
# Change to your installation path
```

## What Changed

**Old approach** (deprecated):
- ❌ Copied .gha to `%APPDATA%\Grasshopper\Libraries\`
- ❌ Required manual reloading
- ❌ Multiple confusing scripts
- ❌ Python script issues

**New approach**:
- ✅ One script: `test.ps1`
- ✅ Uses `RHINO_PACKAGE_DIRS` (VS approach)
- ✅ No file copying
- ✅ Clean and simple

## Full Documentation

See [scripts/README.md](scripts/README.md) for complete parameter reference.

## References

- [McNeel Forum - Load Grasshopper File](https://discourse.mcneel.com/t/load-grasshopper-file-from-rhino-command/127747)
- [Grasshopper Player Docs](https://www.rhino3d.com/features/grasshopper/player/)
- [Rhino Command Line](https://discourse.mcneel.com/t/open-and-run-grasshopper-from-a-batch-file/80261)
