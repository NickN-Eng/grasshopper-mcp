# Grasshopper MCP - Test Script

## One-Time Setup Required

Before using the script, add your build folder to Grasshopper:

```
1. Open Grasshopper
2. Run: GrasshopperDeveloperSettings
3. Add folder: C:\Users\nniem\source\repos\grasshopper-mcp\GH_MCP\GH_MCP\bin\Debug\net48
4. Restart Grasshopper
```

See [../SETUP.md](../SETUP.md) for detailed instructions.

## Quick Start

```powershell
# Build and test (opens default test file)
.\scripts\test.ps1

# Skip build if already built
.\scripts\test.ps1 -SkipBuild

# Keep Rhino open for inspection
.\scripts\test.ps1 -KeepOpen

# Use Grasshopper Player (headless)
.\scripts\test.ps1 -UsePlayer
```

## What It Does

1. ✅ Builds the GH_MCP solution (Debug by default)
2. ✅ Launches Rhino + Grasshopper
3. ✅ Opens default test file: `gh_scripts/gh_mcp_load.gh`
4. ✅ Plugin loads from your GrasshopperDeveloperSettings
5. ✅ **NO environment variables!**
6. ✅ **NO conflicts!**

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-GrasshopperFile` | `gh_scripts\gh_mcp_load.gh` | Path to .gh file to open |
| `-Configuration` | `Debug` | Build configuration (Debug/Release) |
| `-SkipBuild` | `false` | Skip the build step |
| `-UsePlayer` | `false` | Use Grasshopper Player (headless) |
| `-KeepOpen` | `false` | Keep Rhino open (default: auto-close after 30s) |
| `-TimeoutSeconds` | `30` | Seconds before auto-close (if not KeepOpen) |

## Examples

### Standard workflow (AI iteration)
```powershell
# Make code changes, then:
.\scripts\test.ps1

# Rhino launches, runs test, closes automatically
# Inspect results in Grasshopper canvas
```

### Custom test file
```powershell
.\scripts\test.ps1 -GrasshopperFile "tests\my-test.gh" -KeepOpen
```

### Headless testing
```powershell
.\scripts\test.ps1 -UsePlayer
```

### Release build
```powershell
.\scripts\test.ps1 -Configuration Release -KeepOpen

# Note: You'll need to add the Release folder to GrasshopperDeveloperSettings too
```

## How It Works (GrasshopperDeveloperSettings Approach)

1. **Build**: Compiles to `GH_MCP\GH_MCP\bin\{Configuration}\net48\`
2. **Launch**: `Rhino.exe /nosplash /netfx /runscript="..."`
3. **Load**: Grasshopper loads plugin from your developer folders
4. **Test**: Opens specified .gh file

**No file copying! No environment variables!**

The plugin is loaded from GrasshopperDeveloperSettings that you configured once.

## Default Test File

`gh_scripts/gh_mcp_load.gh` - Default Grasshopper definition for testing GH_MCP

This file should contain components that test your MCP functionality.

## For AI Agents

### Typical workflow:
```powershell
# 1. Edit C# code
# (AI makes changes)

# 2. Test
.\scripts\test.ps1

# 3. Grasshopper opens with test file
# 4. Inspect results
# 5. Iterate
```

### Fast iteration:
```powershell
# Keep Rhino open and manually reload
.\scripts\test.ps1 -KeepOpen

# Make changes
# Rebuild: .\scripts\test.ps1 -SkipBuild
# Reload in Rhino
```

## Debugging

### With Visual Studio:

1. Start the test script:
   ```powershell
   .\scripts\test.ps1 -KeepOpen
   ```

2. In Visual Studio:
   - Debug → Attach to Process
   - Select Rhino.exe
   - Click Attach

3. Set breakpoints and debug!

### Or use F5 in Visual Studio:

The `launchSettings.json` is configured for debugging.

**Note**: Visual Studio F5 uses `RHINO_PACKAGE_DIRS` which is separate from GrasshopperDeveloperSettings. Both can coexist - they're for different purposes:
- **GrasshopperDeveloperSettings**: Persistent developer folders
- **RHINO_PACKAGE_DIRS**: Temporary (VS debugging only)

## Cleanup Old Approach

If you previously copied the .gha to Grasshopper\Libraries:

```powershell
# Remove old copied file
Remove-Item "$env:APPDATA\Grasshopper\Libraries\GH_MCP.gha" -ErrorAction SilentlyContinue
```

## Troubleshooting

### "Plugin not loading"
- Check GrasshopperDeveloperSettings includes correct folder
- Restart Grasshopper
- Make sure folder path matches your Configuration (Debug vs Release)

### "Multiple versions loading" / Conflicts
- Remove duplicate paths from GrasshopperDeveloperSettings
- Only ONE path should point to your build output

### "Plugin not found"
```powershell
# Build first
.\scripts\test.ps1 -SkipBuild:$false
```

### "Grasshopper file not found"
```powershell
# Check the path
ls gh_scripts\gh_mcp_load.gh

# Or specify a different file
.\scripts\test.ps1 -GrasshopperFile "path\to\your\file.gh"
```

### "Rhino not found"
Edit line 116 in `test.ps1`:
```powershell
$RhinoExe = "C:\Program Files\Rhino 8\System\Rhino.exe"
# Change to your Rhino installation path
```

## Why This Approach?

**Previous approach**: Used `RHINO_PACKAGE_DIRS` environment variable
- ❌ Conflicts with manually configured developer folders
- ❌ User had multiple versions loading
- ❌ Confusion about what loads from where

**New approach**: Uses `GrasshopperDeveloperSettings`
- ✅ One-time setup
- ✅ No environment variables
- ✅ No conflicts
- ✅ Clean and simple
- ✅ Matches McNeel's recommended workflow

## References

- [Your First Component - Rhino Developer Docs](https://developer.rhino3d.com/guides/grasshopper/your-first-component-windows/)
- [GrasshopperDeveloperSettings - McNeel Forum](https://discourse.mcneel.com/t/debugging-c-grasshopper-components/40600)
- [Grasshopper Player](https://www.rhino3d.com/features/grasshopper/player/)
