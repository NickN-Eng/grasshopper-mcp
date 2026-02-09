# AI Testing Workflow - Summary & Recommendations

## Current Status

I've created a complete AI-driven testing infrastructure, but discovered that Rhino 8's Python script automation has compatibility issues. Here are the practical solutions:

## Recommended Approach: "Rhino Running" Workflow

**Best for**: Rapid AI iteration during development

### Setup (One Time)
1. Open Rhino
2. Load Grasshopper (`_Grasshopper` command)
3. Ensure GH_MCP plugin is loaded
4. Keep Rhino/Grasshopper open in the background

### AI Testing Loop
```powershell
# Build, deploy, and test (assumes Rhino is running)
.\scripts\test-with-rhino-running.ps1 -TestFile "tests\commands\smoke-test.txt"
```

**Workflow**:
1. AI edits code
2. AI runs the script (builds, deploys, runs tester)
3. Console tester connects to Grasshopper via named pipe
4. Results saved to `logs/test-results-[timestamp].json`
5. AI reads results and iterates

**Pros**:
- Fast (no Rhino startup overhead)
- Reliable (no Python script issues)
- Practical for development

**Cons**:
- Requires Rhino to be open
- Manual step to reload plugin after rebuild

---

## Alternative: Visual Studio Debug Approach

Use the launch configuration already in your project:

### File: [GH_MCP\GH_MCP\Properties\launchSettings.json](GH_MCP/GH_MCP/Properties/launchSettings.json)

```json
{
  "profiles": {
    "Rhino 8 - netcore": {
      "commandName": "Executable",
      "executablePath": "C:\\Program Files\\Rhino 8\\System\\Rhino.exe",
      "commandLineArgs": "/netcore /runscript=\"_Grasshopper\"",
      "environmentVariables": {
        "RHINO_PACKAGE_DIRS": "$(ProjectDir)$(OutputPath)\\"
      }
    }
  }
}
```

**For AI automation**:
```powershell
# Build
dotnet build GH_MCP/GH_MCP.sln -c Release

# Launch Rhino with Grasshopper (mimics VS debug)
& "C:\Program Files\Rhino 8\System\Rhino.exe" /nosplash /runscript="_Grasshopper"

# Manually run tests in the open Grasshopper instance
```

**Pros**:
- Uses proven Visual Studio workflow
- No Python script issues
- Environment variables can specify plugin path

**Cons**:
- Still manual test triggering
- Rhino stays open

---

## Future: Grasshopper Player (Headless)

For truly headless execution, use Grasshopper Player with `.gh` files:

```powershell
& "C:\Program Files\Rhino 8\System\Rhino.exe" `
    /nosplash `
    /notemplate `
    /runscript="_-GrasshopperPlayer test-definition.gh _Exit"
```

**Requirements**:
- Create `.gh` test definitions with expected outputs
- Use Grasshopper Player to solve them
- Verify outputs programmatically

**Status**: Not yet implemented (would need `.gh` test files)

---

## What Was Created

### ✅ Working Scripts

1. **`scripts/test-with-rhino-running.ps1`** ⭐ RECOMMENDED
   - Build, deploy, test with Rhino already running
   - Most practical for AI iteration
   - Use this one!

2. **`scripts/build-and-deploy.ps1`**
   - Quick build & deploy
   - Use for rapid development
   - Supports watch mode (`-WatchMode`)

### ⚠️  Scripts with Limitations

3. **`scripts/test-ai-loop.ps1`**
   - Attempts to launch/exit Rhino automatically
   - **Issue**: Python script not recognized by Rhino 8
   - **Workaround**: Use Rhino commands instead of Python

4. **`scripts/run-mcp-tests.ps1`**
   - Comprehensive MCP test suite
   - **Same issue** as test-ai-loop.ps1
   - **Fix needed**: Convert Python automation to Rhino commands

### ✅ Test Files

- `tests/commands/smoke-test.txt` - Working
- `tests/commands/basic-test.txt` - Working
- `tests/commands/connection-test.txt` - Working
- `tests/McpTestRunner.cs` - Working (programmatic tests)

### ✅ Console Tester

- `GH_MCP.Tester.Console` - Fully working
- Connects to Grasshopper via named pipe
- Runs command files or interactive mode

---

## Quick Start (RIGHT NOW)

### 1. Build the solution
```powershell
dotnet build GH_MCP/GH_MCP.sln -c Release
```

### 2. Start Rhino & Grasshopper
- Open Rhino
- Run `_Grasshopper` command
- Plugin should load from `%APPDATA%\Grasshopper\Libraries\GH_MCP.gha`

### 3. Run a quick test
```powershell
.\scripts\test-with-rhino-running.ps1 -TestFile "tests\commands\smoke-test.txt"
```

This will:
1. Build the project (if not skipped)
2. Deploy GH_MCP.gha
3. Wait for you to confirm Rhino is ready
4. Run the console tester
5. Show results

### 4. Check results
```powershell
# View latest results
cat logs\test-results-*.json | Select-Object -Last 1 | ConvertFrom-Json

# View test log
cat logs\test-*.log | Select-Object -Last 1
```

---

## Fixing the Automated Scripts

To make `test-ai-loop.ps1` and `run-mcp-tests.ps1` fully automated, I need to:

1. **Replace Python scripts with Rhino command sequences**
   - Use `/runscript` with Rhino commands instead of Python
   - Example: `/runscript="_Grasshopper _-RunTest _Exit"`

2. **Create a Grasshopper component for test triggering**
   - Component that reads test file and executes commands
   - Triggered by Rhino command
   - Outputs results to file

3. **Or use Grasshopper Player**
   - Create `.gh` test definitions
   - Use Player to solve headlessly
   - Verify outputs

**Would you like me to implement one of these approaches?**

---

## For AI Agents (Claude, etc.)

### Recommended Workflow

```powershell
# Prerequisites: Rhino + Grasshopper are running

# 1. Edit code
# (AI makes changes to .cs files)

# 2. Test
.\scripts\test-with-rhino-running.ps1 -SkipBuild:$false

# 3. Check result
$LASTEXITCODE  # 0 = success, 1 = failure

# 4. Read details
$results = cat logs\test-results-*.json | Select-Object -Last 1 | ConvertFrom-Json
$results | ConvertTo-Json

# 5. Iterate based on results
```

### Fast Iteration (Rhino already running, build only)

```powershell
# Watch mode: auto-rebuild on file changes
.\scripts\build-and-deploy.ps1 -WatchMode

# In another terminal: run tests manually
.\scripts\test-with-rhino-running.ps1 -SkipBuild
```

---

## Next Steps

Choose one:

### A. Use the "Rhino Running" workflow now ⭐
- Start using `test-with-rhino-running.ps1`
- Practical and works immediately
- Manual Rhino startup, automated testing

### B. Fix automated scripts for true headless execution
- Convert Python automation to Rhino commands
- Or implement Grasshopper Player approach
- Fully autonomous, no manual steps

### C. Hybrid: Rhino command automation
- Use launchSettings.json approach
- Launch Rhino with `/runscript`
- Environment variables for plugin path
- Simpler than Python, more automated than "Rhino Running"

**Which approach would you like me to implement/improve?**

---

## Files Created

All documentation and scripts are in place:

- ✅ `scripts/test-with-rhino-running.ps1` - WORKS NOW
- ✅ `scripts/build-and-deploy.ps1` - WORKS
- ⚠️  `scripts/test-ai-loop.ps1` - Needs Python→Rhino command conversion
- ⚠️  `scripts/run-mcp-tests.ps1` - Needs Python→Rhino command conversion
- ✅ `tests/commands/*.txt` - Test command files
- ✅ `tests/McpTestRunner.cs` - Programmatic test suite
- ✅ `docs/ai-testing-guide.md` - Comprehensive guide
- ✅ `docs/testing-architecture.md` - System architecture
- ✅ `AI-TESTING-QUICKSTART.md` - Quick start guide

**Ready to test with the working script whenever you are!**
