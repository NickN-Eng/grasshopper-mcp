# Grasshopper MCP - One-Time Setup

## Plugin Loading Setup (Required Once)

The plugin needs to be added to Grasshopper's developer folders so it loads automatically.

### Steps:

1. **Open Rhino and Grasshopper**
   ```
   Launch Rhino → Type "Grasshopper" command
   ```

2. **Run GrasshopperDeveloperSettings**
   ```
   In Grasshopper window → Type "GrasshopperDeveloperSettings"
   ```

3. **Add Your Build Folder**

   Click **"Add folder"** and navigate to:
   ```
   C:\Users\nniem\source\repos\grasshopper-mcp\GH_MCP\GH_MCP\bin\Debug\net48
   ```

   Or for Release builds:
   ```
   C:\Users\nniem\source\repos\grasshopper-mcp\GH_MCP\GH_MCP\bin\Release\net48
   ```

   **Recommendation**: Add the **Debug** folder for development.

4. **Restart Grasshopper**
   ```
   Close and reopen Grasshopper
   Your plugin will now load automatically!
   ```

## Verify Setup

After restarting Grasshopper:
- Your GH_MCP components should appear in the component panel
- No manual loading needed
- Plugin automatically updates when you rebuild

## Switch Between Debug/Release

You can add BOTH folders to GrasshopperDeveloperSettings:
- Debug: For development and debugging
- Release: For testing final builds

Grasshopper will load from whichever folder has a newer/matching build.

**Or** switch between them:
1. Remove one folder
2. Add the other
3. Restart Grasshopper

## Test Script

Once setup is complete, run:
```powershell
.\scripts\test.ps1
```

The script will:
- ✅ Build your project
- ✅ Launch Rhino + Grasshopper
- ✅ Open test file
- ✅ Plugin loads from your configured developer folder
- ✅ No environment variables or file copying!

## Debugging with Visual Studio

### Option 1: Attach to Process (Recommended)

1. Start Rhino + Grasshopper (via script or manually)
2. In Visual Studio: **Debug → Attach to Process**
3. Find **Rhino.exe**
4. Click **Attach**
5. Set breakpoints and debug!

### Option 2: Launch from Visual Studio (F5)

The `launchSettings.json` is configured to launch Rhino with debugging.

**Note**: Visual Studio uses `RHINO_PACKAGE_DIRS` environment variable, which is **separate** from your GrasshopperDeveloperSettings. This is fine - they can coexist for VS debugging sessions.

## Troubleshooting

### "Plugin not loading"
- Check GrasshopperDeveloperSettings includes your build folder
- Restart Grasshopper
- Check the folder path is correct

### "Multiple versions loading"
- Remove duplicate paths from GrasshopperDeveloperSettings
- Only have ONE path pointing to your build output

### "Old version loading"
- Rebuild your project (the script does this)
- Make sure GrasshopperDeveloperSettings points to the right configuration (Debug vs Release)
- Restart Grasshopper to reload

## Clean Up Old Approach

If you previously copied the .gha to Grasshopper\Libraries:

```powershell
Remove-Item "$env:APPDATA\Grasshopper\Libraries\GH_MCP.gha" -ErrorAction SilentlyContinue
```

This is no longer needed with GrasshopperDeveloperSettings!

## Summary

**One-time**: Add build folder to GrasshopperDeveloperSettings
**Every time**: Run `.\scripts\test.ps1`

That's it! Simple, clean, no conflicts.
