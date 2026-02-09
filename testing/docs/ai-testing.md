# AI-Driven Testing for GH_MCP

## Overview

This document describes the AI-friendly testing infrastructure for the Grasshopper MCP plugin. The system is designed to enable AI agents (like Claude) to:

1. Execute MCP commands
2. Read log files to observe results
3. Verify document state
4. Iterate on failures

## Architecture

```
┌─────────────┐     ┌──────────────────┐     ┌─────────────┐
│   AI Agent  │────▶│  Test Harness    │────▶│  GH_MCP     │
│   (Claude)  │     │  (Console/WPF)   │     │  (Plugin)   │
└─────────────┘     └──────────────────┘     └─────────────┘
       │                    │                       │
       │                    ▼                       ▼
       │            ┌──────────────┐        ┌─────────────┐
       └───────────▶│  Log Files   │        │ Grasshopper │
         read       │  (logs/)     │        │  Document   │
                    └──────────────┘        └─────────────┘
```

## Log Files

All logs are written to the `logs/` directory at the repo root.

### Log File Types

| File Pattern | Source | Purpose |
|-------------|--------|---------|
| `mcp-client-*.log` | GH_MCP.Client | All request/response pairs |
| `mcp-bridge-*.log` | GH_MCP_Bridge | MCP server activity |
| `tester-console-*.log` | Console Tester | Test session logs |
| `tester-wpf-*.log` | WPF Tester | Test session logs |
| `test-results-*.log` | Test Runner | Human-readable results |
| `test-results-*.json` | Test Runner | Machine-parseable results |

### Log Format

Request/response pairs are logged as JSON:

```json
>>> REQUEST [14:32:15.123] add_component
{
  "timestamp": "2025-01-27 14:32:15.123",
  "direction": "REQUEST",
  "type": "add_component",
  "parameters": {
    "type": "Number Slider",
    "x": 100,
    "y": 100
  }
}

<<< RESPONSE [14:32:15.456] add_component (333.0ms)
{
  "timestamp": "2025-01-27 14:32:15.456",
  "direction": "RESPONSE",
  "type": "add_component",
  "elapsed_ms": 333.0,
  "response": {
    "success": true,
    "data": {
      "componentId": "abc-123-def"
    }
  }
}
```

### Test Results JSON Format

```json
{
  "timestamp": "2025-01-27 14:32:15",
  "summary": {
    "passed": 10,
    "failed": 2,
    "total": 12
  },
  "results": [
    {
      "name": "Add Component: Number Slider",
      "passed": true,
      "details": "Added with ID: abc-123-def"
    },
    {
      "name": "Connect Components",
      "passed": false,
      "details": "Error: Source component not found"
    }
  ]
}
```

## AI Testing Workflow

### Step 1: Run Tests

```bash
# Using console tester with command file
GH_MCP.Tester.Console.exe tests/commands/basic-test.txt

# Or compile and run the test runner
cd tests
dotnet run McpTestRunner.cs ../logs
```

### Step 2: Read Results

AI reads the log files:

```bash
# Latest test results
cat logs/test-results-*.json | tail -1

# Latest client log
cat logs/mcp-client-*.log | tail -100
```

### Step 3: Analyze Failures

For each failure, AI can:
1. Check the request that was sent
2. Check the response received
3. Verify document state with `get_all_components`
4. Retry with modified parameters

### Step 4: Verify Document State

```bash
# Get current state via console tester
echo "list" | GH_MCP.Tester.Console.exe

# Or check connections
echo "connections" | GH_MCP.Tester.Console.exe
```

## Verification Commands

The following verification commands are available in the GH_MCP plugin for AI-driven testing:

### export_document_state
Returns a full JSON snapshot of all components, connections, and values.

```json
{
  "exportTime": "2025-01-27 14:32:15.123",
  "documentName": "Untitled",
  "documentPath": "",
  "componentCount": 3,
  "connectionCount": 2,
  "components": [...],
  "connections": [...]
}
```

### assert_component_exists
Asserts that a component with the given ID exists.

Parameters: `componentId` (string)

```json
{ "passed": true, "componentId": "abc-123", "componentType": "GH_NumberSlider", "componentName": "Slider" }
```

### assert_connection_exists
Asserts that a connection exists between two components.

Parameters: `sourceId`, `targetId`, `sourceParam?`, `targetParam?`

```json
{ "passed": true, "sourceId": "abc-123", "targetId": "def-456", "sourceParam": "Number", "targetParam": "A" }
```

### assert_component_count
Asserts that the document has a specific number of components.

Parameters: `expected` (integer)

```json
{ "passed": true, "expected": 3, "actual": 3, "difference": 0 }
```

### get_document_hash
Returns a SHA256 hash of the document state for quick comparison.

```json
{ "hash": "a1b2c3d4...", "componentCount": 3, "timestamp": "2025-01-27 14:32:15.123" }
```

### Console Tester Commands

The Console tester provides shortcuts for these verification commands:

```
export                    - Export full document state as JSON
assert_exists <id>        - Assert component exists
assert_connection <s> <t> - Assert connection exists
assert_count <n>          - Assert component count
hash                      - Get document state hash
```

## Future Enhancements

### Validation Script Pattern

For complex verification, create validation scripts:

```csharp
// ValidateAddition.cs - Validates an addition pattern
var components = await client.GetAllComponentsAsync();
var sliders = components.Where(c => c.Type == "Number Slider").ToList();
var addition = components.FirstOrDefault(c => c.Type == "Addition");

// Assertions
Assert(sliders.Count >= 2, "Should have at least 2 sliders");
Assert(addition != null, "Should have addition component");

var connections = await client.GetConnectionsAsync();
var toAddition = connections.Where(c => c.TargetId == addition.Id).ToList();

Assert(toAddition.Count >= 2, "Addition should have 2 inputs connected");
```

## Environment Variables

| Variable | Purpose | Default |
|----------|---------|---------|
| `GRASSHOPPER_HOST` | Grasshopper plugin host | localhost |
| `GRASSHOPPER_PORT` | Grasshopper plugin port | 8080 |
| `GRASSHOPPER_MCP_LOG_DIR` | Log file directory | ./logs |

## Tips for AI Agents

1. **Always clear first**: Start tests with `clear_document` for clean state
2. **Log IDs**: Component IDs change each run - parse them from add responses
3. **Check success**: Always verify `response.success == true`
4. **Read JSON results**: Prefer `.json` files over `.log` for parsing
5. **Use timestamps**: Log files include timestamps for ordering events
6. **Iterate quickly**: Small test batches are easier to debug than large ones
