# AI-Driven Testing - Quick Start Guide

Your Grasshopper MCP project now has a complete AI-driven testing workflow! This guide gets you started in 5 minutes.

## What Was Created

A complete automated testing system that allows AI (like Claude) to:
1. ✅ Build your C# code
2. ✅ Deploy to Grasshopper
3. ✅ Launch Rhino headlessly
4. ✅ Run tests automatically
5. ✅ Read results and iterate
6. ✅ All without human intervention

## Quick Test (30 seconds)

Verify everything works:

```powershell
# From repo root
.\scripts\test-ai-loop.ps1 -TestFile "tests\commands\smoke-test.txt"
```

If successful, you'll see:
```
✓ Build succeeded
✓ Plugin deployed
✓ Rhino test execution completed
✓ All tests passed!
```

Results are in `logs/test-results-[timestamp].json`.

## Available Test Scripts

### 1. Smoke Test (5 seconds)
Quick validation after every code change:
```powershell
.\scripts\test-ai-loop.ps1 -TestFile "tests\commands\smoke-test.txt"
```

### 2. Basic Functional Test (15 seconds)
Standard test suite with multiple components:
```powershell
.\scripts\test-ai-loop.ps1 -TestFile "tests\commands\basic-test.txt"
```

### 3. Full MCP Test Suite (60 seconds)
Comprehensive programmatic tests:
```powershell
.\scripts\run-mcp-tests.ps1
```

### 4. Build & Deploy Only (10 seconds)
For rapid development iteration:
```powershell
.\scripts\build-and-deploy.ps1
```

## AI Workflow Example

Here's how Claude (or another AI) can use this:

```powershell
# 1. AI edits some C# code in GH_MCP/
# (makes changes to fix a bug or add a feature)

# 2. AI runs smoke test
.\scripts\test-ai-loop.ps1 -TestFile "tests\commands\smoke-test.txt"

# 3. AI checks exit code
if ($LASTEXITCODE -eq 0) {
    Write-Host "Tests passed! ✓"
} else {
    Write-Host "Tests failed - reading logs..."
    cat logs\test-*.log | Select-Object -Last 1
}

# 4. AI reads structured results
$results = cat logs\test-results-*.json | Select-Object -Last 1 | ConvertFrom-Json
$results.summary

# 5. AI iterates based on results
# If failures: analyze, fix, re-run
# If success: commit changes
```

## What Gets Tested

### Command-Based Tests (via console tester)
- Component creation (`add`)
- Component listing (`list`)
- Connections (`wire`)
- Search (`search`)
- Parameters (`params`)
- Assertions (`assert_count`, `assert_exists`, `assert_connection`)
- State export (`export`)

### Programmatic Tests (via McpTestRunner.cs)
- Named pipe connection
- Document operations (clear, save, load)
- All component CRUD operations
- Complex wiring scenarios
- Search and validation
- Parameter introspection
- Error handling

## Reading Results

### Exit Codes
```powershell
.\scripts\test-ai-loop.ps1
echo $LASTEXITCODE
# 0 = success
# 1 = failure (build or tests)
```

### JSON Results
```powershell
# Read latest results
$results = cat logs\test-results-*.json | Select-Object -Last 1 | ConvertFrom-Json

# Summary
$results.summary
# Output: @{passed=12; failed=0; total=12}

# View failures only
$results.results | Where-Object { -not $_.Passed }
```

### Log Files
```powershell
# Latest test log
cat logs\test-*.log | Select-Object -Last 1

# Latest build log (if build failed)
cat logs\build-*.log | Select-Object -Last 1
```

## TDD Workflow

Following the project's AI-driven TDD guidelines:

### 1. Write Test First
```csharp
// In tests/McpTestRunner.cs
private async Task TestMyNewFeature()
{
    var response = await _client.MyNewFeatureAsync();
    RecordResult("My New Feature", response.Success, response.Error);
}
```

Add to `RunAllTests()`:
```csharp
await TestMyNewFeature();
```

### 2. Run Tests (Should Fail)
```powershell
.\scripts\run-mcp-tests.ps1
# Expected: "My New Feature" test fails
```

### 3. Implement Feature
```csharp
// Edit GH_MCP code to implement the feature
```

### 4. Run Tests (Should Pass)
```powershell
.\scripts\run-mcp-tests.ps1
# Expected: All tests pass
```

### 5. Commit
```bash
git add .
git commit -m "feat: Add new feature with tests"
```

## Directory Structure

```
grasshopper-mcp/
├── scripts/
│   ├── test-ai-loop.ps1          # Main AI testing script
│   ├── run-mcp-tests.ps1         # Comprehensive test suite
│   ├── build-and-deploy.ps1      # Quick build & deploy
│   └── README.md                 # Script documentation
├── tests/
│   ├── commands/
│   │   ├── smoke-test.txt        # Minimal test
│   │   ├── basic-test.txt        # Standard test
│   │   └── connection-test.txt   # Wiring test
│   └── McpTestRunner.cs          # Programmatic test suite
├── logs/ (git-ignored)
│   ├── README.md
│   ├── build-*.log               # Build output
│   ├── test-*.log                # Test execution logs
│   └── test-results-*.json       # Structured results
├── docs/
│   ├── ai-testing-guide.md       # Comprehensive AI guide
│   └── testing-architecture.md   # System architecture
└── GH_MCP/
    ├── GH_MCP/                   # Main plugin (what you edit)
    ├── GH_MCP.Client/            # Client library for tests
    └── GH_MCP.Tester.Console/    # Console test tool
```

## Common Issues

### "Rhino not found"
Edit script to match your Rhino installation:
```powershell
# In test-ai-loop.ps1, around line 100
$RhinoExe = "C:\Program Files\Rhino 8\System\Rhino.exe"
# Change to your path
```

### Tests timeout
Increase timeout:
```powershell
.\scripts\test-ai-loop.ps1 -TimeoutSeconds 120
```

### Want to inspect canvas after test
Keep Rhino open:
```powershell
.\scripts\test-ai-loop.ps1 -KeepRhinoOpen
```

### Build succeeded but tests fail
Check the test log:
```powershell
cat logs\test-*.log | Select-Object -Last 1
```

Common causes:
- Plugin didn't load
- Named pipe connection failed
- Component names misspelled in test

## Advanced Usage

### Watch Mode (Auto-rebuild)
```powershell
# Auto-rebuild on .cs file changes
.\scripts\build-and-deploy.ps1 -WatchMode
# Keep this running, manually test in Rhino
```

### Custom Test File
```powershell
# Create your own test
New-Item tests\commands\my-test.txt
# Add commands (see basic-test.txt for examples)

# Run it
.\scripts\test-ai-loop.ps1 -TestFile "tests\commands\my-test.txt"
```

### Debug Build
```powershell
.\scripts\test-ai-loop.ps1 -Configuration Debug
```

### Skip Build (If Already Built)
```powershell
.\scripts\test-ai-loop.ps1 -SkipBuild
```

## Next Steps

1. **Try it now**: Run the smoke test
   ```powershell
   .\scripts\test-ai-loop.ps1 -TestFile "tests\commands\smoke-test.txt"
   ```

2. **Make a change**: Edit some C# code

3. **Test again**: See the automated workflow in action

4. **Read the guides**:
   - [scripts/README.md](scripts/README.md) - All script options
   - [docs/ai-testing-guide.md](docs/ai-testing-guide.md) - Complete AI guide
   - [docs/testing-architecture.md](docs/testing-architecture.md) - How it works

## Documentation

- **Quick reference**: This file
- **Script reference**: [scripts/README.md](scripts/README.md)
- **AI workflow guide**: [docs/ai-testing-guide.md](docs/ai-testing-guide.md)
- **Architecture**: [docs/testing-architecture.md](docs/testing-architecture.md)
- **Development guide**: [docs/dev-quickstart.md](docs/dev-quickstart.md)

## Success Criteria

You'll know it's working when:
1. ✅ `.\scripts\test-ai-loop.ps1` exits with code 0
2. ✅ `logs/test-results-*.json` shows `"success": true`
3. ✅ AI can read logs and iterate autonomously
4. ✅ Changes to code are automatically tested

## Questions?

Check the detailed guides in `docs/` or the script documentation in `scripts/README.md`.

Happy testing! 🚀
