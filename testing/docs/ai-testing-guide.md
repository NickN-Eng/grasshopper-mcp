# AI-Driven Testing Guide for Grasshopper MCP

This guide explains how AI agents can autonomously test and iterate on the Grasshopper MCP codebase.

## Overview

Grasshopper MCP now supports fully automated testing via PowerShell scripts that:
1. Build the C# plugin
2. Deploy to Rhino/Grasshopper
3. Launch Rhino headlessly
4. Execute test commands
5. Capture results in machine-readable logs
6. Exit Rhino automatically

This enables AI to make changes and verify them without human intervention.

## Quick Start for AI

### Minimal workflow:
```powershell
# 1. Make code changes
# (edit .cs files)

# 2. Run smoke test
.\scripts\test-ai-loop.ps1 -TestFile "tests\commands\smoke-test.txt"

# 3. Check if successful
echo $LASTEXITCODE  # 0 = success, 1 = failure

# 4. Read results
cat logs\test-results-*.json | Select-Object -Last 1 | ConvertFrom-Json
```

### Full test workflow:
```powershell
# Run comprehensive test suite
.\scripts\run-mcp-tests.ps1

# Parse JSON results
$results = cat logs\test-results-*.json | Select-Object -Last 1 | ConvertFrom-Json

# Check summary
$results.summary.passed  # number of passed tests
$results.summary.failed  # number of failed tests

# View individual test results
$results.results | Where-Object { -not $_.Passed }
```

## Test Levels

### Level 1: Smoke Test (5 seconds)
**When to use**: After every code change, before committing

```powershell
.\scripts\test-ai-loop.ps1 -TestFile "tests\commands\smoke-test.txt"
```

Tests:
- Plugin loads
- Basic component creation works
- Canvas operations work

### Level 2: Functional Test (15 seconds)
**When to use**: Before pushing changes

```powershell
.\scripts\test-ai-loop.ps1 -TestFile "tests\commands\basic-test.txt"
```

Tests:
- Component CRUD
- Search
- Parameters
- Connections (basic)

### Level 3: Full MCP Test Suite (60 seconds)
**When to use**: Before releases, after major changes

```powershell
.\scripts\run-mcp-tests.ps1
```

Tests everything via McpTestRunner.cs:
- Connection/ping
- Document operations
- All component types
- Complex wiring scenarios
- Search and validation
- Parameter introspection

## Reading Test Results

### Exit codes
```powershell
.\scripts\test-ai-loop.ps1
echo $LASTEXITCODE

# 0 = all tests passed
# 1 = build failed OR tests failed
```

### JSON results format
```json
{
  "timestamp": "20260130-143022",
  "configuration": "Release",
  "testFile": "tests\\commands\\smoke-test.txt",
  "buildLog": "logs\\build-20260130-143022.log",
  "testLog": "logs\\test-20260130-143022.log",
  "success": true,
  "logContent": "..."
}
```

### MCP test results format
```json
{
  "timestamp": "2026-01-30 14:30:22",
  "summary": {
    "passed": 12,
    "failed": 0,
    "total": 12
  },
  "results": [
    {
      "Name": "Connection Test",
      "Passed": true,
      "Details": "Connected successfully"
    },
    {
      "Name": "Add Component: Number Slider",
      "Passed": true,
      "Details": "Added with ID: abc123"
    }
  ]
}
```

## AI Decision Tree

```
┌─────────────────────┐
│  AI makes changes   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────────┐
│ Run smoke test          │
│ (test-ai-loop.ps1)      │
└──────────┬──────────────┘
           │
           ├─── PASS ──────────► Continue development
           │
           └─── FAIL
                 │
                 ▼
           ┌────────────────────┐
           │ Read build log?    │
           └──────┬─────────────┘
                  │
                  ├── Build error ──► Fix compilation
                  │                   └─► Re-run
                  │
                  └── Test error
                        │
                        ▼
                  ┌──────────────────┐
                  │ Read test log    │
                  │ Analyze failure  │
                  └────┬─────────────┘
                       │
                       ├── Plugin didn't load ──► Check deployment
                       ├── Component error ──────► Fix logic
                       ├── Assertion failed ─────► Fix behavior
                       └── Timeout ──────────────► Optimize/increase timeout
                             │
                             ▼
                       Fix code & re-run
```

## Common Scenarios

### Scenario 1: Fixing a bug
```powershell
# 1. AI identifies bug in ComponentCreator.cs
# 2. AI edits the file
# 3. Run quick test
.\scripts\test-ai-loop.ps1 -TestFile "tests\commands\smoke-test.txt"

# 4. If it passes, run full suite
.\scripts\run-mcp-tests.ps1

# 5. Read results
cat logs\test-results-*.json | Select-Object -Last 1 | ConvertFrom-Json
```

### Scenario 2: Adding new feature
```powershell
# 1. AI implements new command handler
# 2. AI adds test case to McpTestRunner.cs
# 3. Build and run
.\scripts\run-mcp-tests.ps1

# 4. Check new test passed
$results = cat logs\test-results-*.json | Select-Object -Last 1 | ConvertFrom-Json
$results.results | Where-Object { $_.Name -like "*NewFeature*" }
```

### Scenario 3: Refactoring
```powershell
# 1. AI refactors code
# 2. Ensure no regressions
.\scripts\run-mcp-tests.ps1

# 3. Compare pass rate
# All tests should still pass
```

## Debugging Failed Tests

### Build failures
```powershell
# Read the build log
cat logs\build-*.log | Select-Object -Last 1

# Common issues:
# - Syntax errors → fix and re-run
# - Missing references → check .csproj
# - Target framework mismatch → verify net48/net7.0
```

### Test failures
```powershell
# Read test execution log
cat logs\test-*.log | Select-Object -Last 1

# Common issues:
# - "Connection failed" → Bridge not running
# - "Component not found" → Check component name spelling
# - "Timeout" → Increase -TimeoutSeconds
# - "Plugin not loaded" → Check deployment path
```

### Verification commands
The console tester supports assertion commands for tests:

```bash
# In test command file (.txt)
add "Number Slider" 100 100
assert_exists <component_id>
assert_count 1
wire <src_id> <tgt_id>
assert_connection <src_id> <tgt_id>
```

## Advanced: Custom Test Scripts

### Creating a new command-based test
```powershell
# 1. Create test file
New-Item -Path "tests\commands\my-feature-test.txt" -ItemType File

# 2. Add commands (see basic-test.txt for syntax)
@"
clear
add "MyNewComponent" 100 100
assert_count 1
export
"@ | Out-File "tests\commands\my-feature-test.txt"

# 3. Run it
.\scripts\test-ai-loop.ps1 -TestFile "tests\commands\my-feature-test.txt"
```

### Adding programmatic tests
```csharp
// Edit tests/McpTestRunner.cs

private async Task TestMyNewFeature()
{
    var testName = "My New Feature Test";
    try
    {
        var response = await _client.MyNewFeatureAsync(params);
        RecordResult(testName, response.Success,
            response.Success ? "Feature works!" : response.Error);
    }
    catch (Exception ex)
    {
        RecordResult(testName, false, $"Exception: {ex.Message}");
    }
}

// Add to RunAllTests():
public async Task RunAllTests()
{
    // ... existing tests ...
    await TestMyNewFeature();
    // ... rest of tests ...
}
```

Then run: `.\scripts\run-mcp-tests.ps1`

## Performance Considerations

### Test execution times
- Smoke test: ~5 seconds
- Basic test: ~15 seconds
- Full MCP suite: ~60 seconds
- Build time: ~10-30 seconds (cached)

### Optimizing for AI iteration speed

**Use smoke tests during active development**:
```powershell
# Fast feedback loop
.\scripts\test-ai-loop.ps1 -TestFile "tests\commands\smoke-test.txt"
```

**Skip builds when only changing test files**:
```powershell
.\scripts\test-ai-loop.ps1 -SkipBuild
```

**Use watch mode during rapid iteration**:
```powershell
# Terminal 1: Watch and auto-build
.\scripts\build-and-deploy.ps1 -WatchMode

# Terminal 2: Run tests manually in Rhino/Grasshopper
# (No automation, but fastest for active development)
```

## Integration with TDD Workflow

Following [CLAUDE.md](../CLAUDE.md) AI-driven TDD requirements:

### 1. Write test first
```csharp
// Add to McpTestRunner.cs
private async Task TestNewFeature()
{
    // This will fail initially
    var response = await _client.NewFeatureAsync();
    RecordResult("New Feature", response.Success, response.Error);
}
```

### 2. Run test (should fail)
```powershell
.\scripts\run-mcp-tests.ps1
# Expected: "New Feature" test fails
```

### 3. Implement feature
```csharp
// Edit GH_MCP code to implement feature
```

### 4. Run test (should pass)
```powershell
.\scripts\run-mcp-tests.ps1
# Expected: "New Feature" test passes
```

### 5. Commit
```bash
git add .
git commit -m "feat: Add new feature with tests"
```

## Best Practices for AI

1. **Always run smoke test after changes**
   - Fast feedback
   - Catches breaking changes early

2. **Read build logs first on failure**
   - Build errors are faster to diagnose
   - Test errors only matter if build succeeds

3. **Use appropriate test level**
   - Code change → smoke test
   - Feature complete → full suite
   - Pre-commit → full suite

4. **Keep test files in version control**
   - Test commands go in `tests/commands/`
   - Programmatic tests in `tests/McpTestRunner.cs`
   - Logs (in `logs/`) are git-ignored

5. **Iterate quickly**
   - Use `-SkipBuild` when only changing tests
   - Use `build-and-deploy.ps1 -WatchMode` for rapid C# iteration

6. **Parse JSON, not text logs**
   - JSON results are structured and reliable
   - Text logs are for human debugging

7. **Check exit codes**
   - `$LASTEXITCODE -eq 0` = success
   - `$LASTEXITCODE -eq 1` = failure
   - Don't rely on text parsing

## Limitations

### What these tests CAN do:
- Verify plugin loads
- Test MCP commands programmatically
- Verify component creation/wiring
- Check search and validation
- Assert document state

### What these tests CANNOT do:
- Test visual appearance
- Verify UI interactions
- Test Rhino geometry computation (use Rhino.Compute for that)
- Run on headless CI without RDP/GUI session

### For geometry verification:
Use Rhino.Compute + Hops to solve definitions and verify outputs:
```csharp
// Future: Use Rhino.Compute to verify a .gh definition solves correctly
var result = await RhinoCompute.Grasshopper.EvaluateDefinition(
    definitionPath,
    inputParameters
);
Assert.Equal(expectedValue, result.Values[0]);
```

## Troubleshooting

### "Rhino.exe not found"
Edit the script and update the path:
```powershell
$RhinoExe = "C:\Program Files\Rhino 8\System\Rhino.exe"
# Change to your Rhino installation path
```

### Tests hang forever
Kill Rhino and increase timeout:
```powershell
Get-Process | Where-Object {$_.Name -eq "Rhino"} | Stop-Process -Force
.\scripts\test-ai-loop.ps1 -TimeoutSeconds 120
```

### "Plugin not loaded" in test log
```powershell
# 1. Check deployment manually
ls "$env:APPDATA\Grasshopper\Libraries\GH_MCP.gha"

# 2. Try manual deployment
.\scripts\build-and-deploy.ps1

# 3. Open Rhino manually and check Grasshopper plugin manager
```

### "Connection failed" errors
The bridge must be running for MCP tests:
```bash
# Terminal 1: Start bridge
cd grasshopper_mcp
python -m grasshopper_mcp.bridge

# Terminal 2: Run tests
.\scripts\run-mcp-tests.ps1
```

For command-based tests, the GH_MCP component must be on the canvas.

## Summary

AI can now fully automate testing:
1. ✅ Build C# code
2. ✅ Deploy to Grasshopper
3. ✅ Launch Rhino headlessly
4. ✅ Execute test commands
5. ✅ Capture structured results
6. ✅ Exit automatically
7. ✅ Iterate based on failures

This enables true TDD with AI as the driver.
