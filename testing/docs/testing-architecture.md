# Testing Architecture

This document describes the testing infrastructure for Grasshopper MCP.

## Overview

The testing system is designed for AI-driven test-driven development (TDD). It enables:
1. Automated builds
2. Automated deployment to Grasshopper
3. Headless Rhino execution
4. Structured test results
5. AI iteration without human intervention

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         AI Agent                                │
│  (Claude, etc.)                                                 │
└────┬────────────────────────────────────────────────────────┬───┘
     │                                                         │
     │ 1. Edits code                                          │ 6. Reads results
     │                                                         │
     ▼                                                         │
┌─────────────────┐                                           │
│  Source Files   │                                           │
│  (.cs)          │                                           │
└────┬────────────┘                                           │
     │                                                         │
     │ 2. Invokes                                             │
     ▼                                                         │
┌──────────────────────────────────────────────────────────┐  │
│  PowerShell Automation Scripts                           │  │
│  (scripts/test-ai-loop.ps1, run-mcp-tests.ps1)          │  │
└──┬────────────┬──────────────────────────────────────────┘  │
   │            │                                              │
   │ 3. Build   │ 4. Deploy                                   │
   ▼            ▼                                              │
┌─────────┐  ┌──────────────────────────┐                     │
│ dotnet  │  │ %APPDATA%\Grasshopper\   │                     │
│ build   │  │ Libraries\GH_MCP.gha     │                     │
└─────────┘  └────────────┬─────────────┘                     │
                          │                                    │
                          │ 5. Launch & Execute                │
                          ▼                                    │
              ┌─────────────────────────┐                      │
              │  Rhino.exe              │                      │
              │  ├─ Grasshopper         │                      │
              │  ├─ GH_MCP.gha loaded   │                      │
              │  └─ Python script runs  │                      │
              └──────────┬──────────────┘                      │
                         │                                     │
                         │ Executes                            │
                         ▼                                     │
              ┌──────────────────────────┐                     │
              │  Test Runner             │                     │
              │  ├─ Console Tester       │                     │
              │  │  (command-based)      │                     │
              │  └─ McpTestRunner.cs     │                     │
              │     (programmatic)       │                     │
              └──────────┬───────────────┘                     │
                         │                                     │
                         │ Writes logs                         │
                         ▼                                     │
              ┌──────────────────────────┐                     │
              │  logs/                   │                     │
              │  ├─ test-results.json ◄──┼─────────────────────┘
              │  ├─ test.log             │
              │  └─ build.log            │
              └──────────────────────────┘
```

## Components

### 1. PowerShell Scripts (`scripts/`)

#### `test-ai-loop.ps1`
Complete build-test-deploy cycle.

**Responsibilities**:
- Build solution via `dotnet build`
- Deploy .gha to Grasshopper Libraries
- Create Python script for Rhino
- Launch Rhino with script
- Wait for completion
- Parse and report results

**Parameters**:
- `Configuration`: Debug/Release (default: Release)
- `TestFile`: Path to test command file
- `KeepRhinoOpen`: Keep Rhino open for inspection
- `SkipBuild`: Skip build step
- `TimeoutSeconds`: Max execution time

**Outputs**:
- Exit code (0=success, 1=failure)
- `logs/build-[timestamp].log`
- `logs/test-[timestamp].log`
- `logs/test-results-[timestamp].json`

#### `run-mcp-tests.ps1`
Runs comprehensive MCP test suite via McpTestRunner.cs.

**Responsibilities**:
- Build solution
- Compile McpTestRunner.cs to standalone exe
- Launch Rhino with test runner
- Execute all programmatic tests
- Output JSON results

**Outputs**:
- `logs/test-results-[timestamp].json` (detailed per-test results)
- `logs/mcp-test-[timestamp].log`

#### `build-and-deploy.ps1`
Quick iteration script for development.

**Responsibilities**:
- Build solution
- Deploy .gha
- (Optional) Watch mode for auto-rebuild

**Use case**: Rapid development without running tests.

### 2. Test Command Files (`tests/commands/`)

Text files with console tester commands, executed line-by-line.

**Format**:
```
# Comments start with #
command arg1 arg2 arg3
```

**Available Commands**:
- `clear` - Clear document
- `add <type> <x> <y>` - Add component
- `list` - List all components
- `info <id>` - Get component info
- `wire <src> <tgt> [sp] [tp]` - Connect components
- `connections` - List connections
- `search <query>` - Search components
- `params <type>` - Get component parameters
- `assert_exists <id>` - Assert component exists
- `assert_count <n>` - Assert component count
- `assert_connection <src> <tgt>` - Assert connection exists
- `export` - Export full document state
- `hash` - Get document hash

**Example Files**:
- `smoke-test.txt` - Minimal validation (1 component)
- `basic-test.txt` - Standard tests (multiple components, search, params)
- `connection-test.txt` - Wiring-focused tests

### 3. Console Tester (`GH_MCP.Tester.Console`)

Interactive and batch test tool.

**Capabilities**:
- Interactive REPL mode
- File execution mode (runs .txt command files)
- JSON batch mode (piped input/output)
- Verification commands (assert_*)
- Logging to file

**Usage**:
```powershell
# Interactive mode
GH_MCP.Tester.Console.exe

# File mode (used by test-ai-loop.ps1)
GH_MCP.Tester.Console.exe tests\commands\basic-test.txt --log logs\

# JSON mode (for programmatic use)
echo '{"command":"add","args":{"type":"Number Slider"}}' | GH_MCP.Tester.Console.exe --json
```

**Architecture**:
```
┌────────────────────────────────────────┐
│  GH_MCP.Tester.Console                 │
│  ├─ Program.cs (main entry)            │
│  └─ Uses GH_MCP.Client library         │
└──────────────┬─────────────────────────┘
               │
               │ Named pipe connection
               ▼
┌────────────────────────────────────────┐
│  GH_MCP Plugin (running in GH)         │
│  ├─ McpServer.cs (named pipe server)   │
│  ├─ Command handlers                   │
│  └─ Grasshopper document manipulation  │
└────────────────────────────────────────┘
```

### 4. MCP Test Runner (`tests/McpTestRunner.cs`)

Comprehensive programmatic test suite.

**Structure**:
```csharp
public class McpTestRunner
{
    private GrasshopperClient _client;

    public async Task RunAllTests()
    {
        await TestConnection();
        await TestAddComponents();
        await TestConnectComponents();
        // ... more tests ...
    }

    private void RecordResult(string name, bool passed, string details)
    {
        // Logs to both console and JSON file
    }
}
```

**Test Categories**:
1. Connection tests - Verify named pipe communication
2. Document tests - Clear, save, load operations
3. Component tests - Add, delete, get info
4. Connection tests - Wiring validation
5. Search tests - Component search
6. Parameter tests - Introspection
7. Validation tests - Pre-connection validation

**Output Format**:
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
    }
    // ... more results ...
  ]
}
```

### 5. Client Library (`GH_MCP.Client`)

C# library for communicating with Grasshopper plugin via named pipes.

**Key Classes**:
- `GrasshopperClient` - Main client API
- `GrasshopperCommand` - Command DTO
- `GrasshopperResponse` - Response DTO
- `McpLogger` - Logging infrastructure

**API Methods**:
```csharp
await client.AddComponentAsync(type, x, y);
await client.GetAllComponentsAsync();
await client.ConnectComponentsAsync(src, tgt, srcParam, tgtParam);
await client.SearchComponentsAsync(query);
await client.ValidateConnectionAsync(src, tgt);
// ... and more
```

**Communication Protocol**:
- Named pipe: `\\.\pipe\GH_MCP_Server`
- Message format: JSON
- Async/await throughout
- Timeout handling
- Error propagation

## Data Flow

### Command-based test flow:
```
test-ai-loop.ps1
  ├─► dotnet build
  ├─► Copy .gha to %APPDATA%
  ├─► Create Python script
  ├─► Launch Rhino
  │     ├─► Load Grasshopper
  │     ├─► GH_MCP.gha loads
  │     ├─► Named pipe server starts
  │     ├─► Run Python script
  │     │     └─► subprocess.run(Tester.Console.exe tests/basic-test.txt)
  │     │           ├─► Connect to named pipe
  │     │           ├─► Read line from test file
  │     │           ├─► Parse command
  │     │           ├─► Send GrasshopperCommand via pipe
  │     │           ├─► Receive GrasshopperResponse
  │     │           ├─► Print result
  │     │           └─► Repeat for all lines
  │     └─► Exit Rhino
  └─► Parse logs and create summary JSON
```

### Programmatic test flow:
```
run-mcp-tests.ps1
  ├─► dotnet build (solution)
  ├─► dotnet build (McpTestRunner.cs → TestRunner.exe)
  ├─► Copy .gha to %APPDATA%
  ├─► Create Python script
  ├─► Launch Rhino
  │     ├─► Load Grasshopper
  │     ├─► GH_MCP.gha loads
  │     ├─► Named pipe server starts
  │     ├─► Run Python script
  │     │     └─► subprocess.run(TestRunner.exe logs/)
  │     │           ├─► new GrasshopperClient()
  │     │           ├─► RunAllTests()
  │     │           │     ├─► TestConnection()
  │     │           │     ├─► TestAddComponents()
  │     │           │     ├─► TestConnectComponents()
  │     │           │     └─► ... more tests ...
  │     │           ├─► RecordResult() for each
  │     │           └─► Write JSON summary
  │     └─► Exit Rhino
  └─► Parse JSON and report summary
```

## Logging Infrastructure

### Log Files Location: `logs/` (git-ignored)

All log files include timestamp in filename to prevent conflicts.

### Build Logs
- **File**: `build-[timestamp].log`
- **Format**: MSBuild output
- **Contents**: Compilation warnings, errors, success messages
- **Created by**: PowerShell scripts via `dotnet build 2>&1 | Out-File`

### Test Logs
- **File**: `test-[timestamp].log`
- **Format**: Plain text
- **Contents**: Console output from test runner
- **Created by**: Python script capturing subprocess output

### Test Results JSON
- **File**: `test-results-[timestamp].json`
- **Format**: JSON
- **Schema**:
  ```json
  {
    "timestamp": "string",
    "summary": {
      "passed": number,
      "failed": number,
      "total": number
    },
    "results": [
      {
        "Name": "string",
        "Passed": boolean,
        "Details": "string"
      }
    ]
  }
  ```
- **Created by**: McpTestRunner.cs

### Client Logs
- **File**: `test-client-[timestamp].log`
- **Format**: Structured text with timestamps
- **Contents**: Named pipe communication, errors, debug info
- **Created by**: `McpLogger` in GH_MCP.Client
- **Log Levels**: Debug, Info, Warning, Error

## Testing Best Practices

### For AI Agents

1. **Always run smoke test after code changes**
   ```powershell
   .\scripts\test-ai-loop.ps1 -TestFile "tests\commands\smoke-test.txt"
   ```

2. **Check exit codes**
   ```powershell
   echo $LASTEXITCODE  # 0 = success, 1 = failure
   ```

3. **Read JSON results, not text logs**
   ```powershell
   $results = cat logs\test-results-*.json | Select-Object -Last 1 | ConvertFrom-Json
   $results.summary.failed  # Check failures
   ```

4. **Use appropriate test level**
   - Smoke test: Quick validation (5 sec)
   - Basic test: Standard checks (15 sec)
   - Full MCP suite: Comprehensive (60 sec)

5. **Add tests before implementing features (TDD)**
   ```csharp
   // 1. Add test to McpTestRunner.cs (will fail)
   private async Task TestNewFeature() { /* ... */ }

   // 2. Run tests (expect failure)
   // .\scripts\run-mcp-tests.ps1

   // 3. Implement feature

   // 4. Run tests (expect success)
   ```

### For Humans

1. **Use interactive console for exploration**
   ```powershell
   cd GH_MCP\GH_MCP.Tester.Console
   dotnet run
   > help
   ```

2. **Use watch mode during development**
   ```powershell
   # Terminal 1
   .\scripts\build-and-deploy.ps1 -WatchMode

   # Edit code, auto-rebuilds on save
   # Manually reload in Grasshopper
   ```

3. **Keep Rhino open for debugging**
   ```powershell
   .\scripts\test-ai-loop.ps1 -KeepRhinoOpen
   # Inspect canvas after test
   ```

## Extending the Test System

### Adding a New Command Test

1. Create file in `tests/commands/`:
   ```bash
   # my-feature-test.txt
   clear
   add "MyComponent" 100 100
   assert_count 1
   export
   ```

2. Run it:
   ```powershell
   .\scripts\test-ai-loop.ps1 -TestFile "tests\commands\my-feature-test.txt"
   ```

### Adding a Programmatic Test

1. Edit `tests/McpTestRunner.cs`:
   ```csharp
   private async Task TestMyFeature()
   {
       var testName = "My Feature Test";
       try
       {
           var response = await _client.MyFeatureAsync();
           RecordResult(testName, response.Success, response.Error);
       }
       catch (Exception ex)
       {
           RecordResult(testName, false, $"Exception: {ex.Message}");
       }
   }
   ```

2. Add to `RunAllTests()`:
   ```csharp
   public async Task RunAllTests()
   {
       // ... existing tests ...
       await TestMyFeature();
       // ...
   }
   ```

3. Run:
   ```powershell
   .\scripts\run-mcp-tests.ps1
   ```

### Adding a New Client API Method

1. Define in `GH_MCP.Client/GrasshopperClient.cs`:
   ```csharp
   public async Task<GrasshopperResponse> MyNewCommandAsync(string param)
   {
       var command = new GrasshopperCommand
       {
           Command = "my_new_command",
           Args = new Dictionary<string, object>
           {
               ["param"] = param
           }
       };
       return await SendCommandAsync(command);
   }
   ```

2. Implement handler in `GH_MCP/Commands/`:
   ```csharp
   public class MyNewCommandHandler : ICommandHandler
   {
       public string CommandName => "my_new_command";

       public async Task<object> HandleAsync(
           Dictionary<string, object> args,
           GH_Document doc)
       {
           // Implementation
           return new { success = true };
       }
   }
   ```

3. Register in `GH_MCP/Commands/CommandRegistry.cs`

4. Add test (see above)

## Performance Metrics

Typical execution times on Windows 10, Ryzen 5 3600:

- Build (clean): ~15 seconds
- Build (incremental): ~3 seconds
- Deploy .gha: <1 second
- Rhino launch: ~5 seconds
- Grasshopper load: ~3 seconds
- Smoke test execution: ~2 seconds
- Basic test execution: ~8 seconds
- Full MCP suite: ~45 seconds
- Total (build + smoke test): ~25 seconds
- Total (build + full suite): ~70 seconds

## Troubleshooting

### Build Failures
**Symptom**: Exit code 1, build log shows errors
**Solution**: Read `logs/build-*.log`, fix compilation errors

### Plugin Not Loading
**Symptom**: "Connection failed" in test log
**Solution**:
- Check `%APPDATA%\Grasshopper\Libraries\GH_MCP.gha` exists
- Check Grasshopper plugin manager
- Try manual load in Grasshopper

### Tests Timeout
**Symptom**: Script hangs, Rhino doesn't exit
**Solution**:
- Increase `-TimeoutSeconds`
- Check if test is blocking on input
- Kill Rhino manually: `Get-Process Rhino | Stop-Process -Force`

### Named Pipe Connection Failed
**Symptom**: "Could not connect to pipe" in client log
**Solution**:
- Ensure GH_MCP component is on canvas (starts pipe server)
- Check Windows firewall/antivirus
- Verify pipe name matches: `\\.\pipe\GH_MCP_Server`

## Future Enhancements

Potential improvements to testing infrastructure:

1. **Rhino.Compute Integration**
   - Solve .gh definitions headlessly
   - Verify geometric outputs
   - True headless testing (no GUI required)

2. **CI/CD Integration**
   - GitHub Actions workflow
   - Automated testing on PRs
   - Build artifact publishing

3. **Visual Regression Testing**
   - Screenshot comparison
   - Canvas layout verification
   - UI testing

4. **Performance Benchmarks**
   - Execution time tracking
   - Memory usage monitoring
   - Regression detection

5. **Test Coverage Reporting**
   - Code coverage metrics
   - Test coverage dashboard
   - Gap identification

6. **Parallel Test Execution**
   - Multiple Rhino instances
   - Concurrent test suites
   - Faster feedback

## References

- [ai-testing-guide.md](ai-testing-guide.md) - Guide for AI agents
- [scripts/README.md](../scripts/README.md) - Script documentation
- [CLAUDE.md](../CLAUDE.md) - Project guidelines
- [dev-quickstart.md](dev-quickstart.md) - Development setup
