# Grasshopper MCP - Testing

This directory contains all testing-related files, documentation, and tools for AI-driven automated testing of the Grasshopper MCP plugin.

## Quick Start

```powershell
# Build and test (launches Rhino + Grasshopper with test file)
.\testing\scripts\test.ps1

# Keep Rhino open for inspection
.\testing\scripts\test.ps1 -KeepOpen
```

See [TESTING.md](TESTING.md) for the main testing guide.

## Directory Structure

```
testing/
├── README.md (this file)            # Testing overview and index
├── TESTING.md                       # Main testing guide
├── AI-TESTING-QUICKSTART.md         # Quick start for AI-driven testing
├── AI-TESTING-SUMMARY.md            # Summary of AI testing workflow
├── QUICKSTART-TESTING.md            # Quick testing commands
├── READY-TO-TEST.md                 # C# script compilation testing status
│
├── scripts/                         # Automated test scripts
│   ├── README.md                    # Script documentation
│   ├── test.ps1                     # Main build & test script
│   ├── AnalyzeDlls.csproj           # DLL analysis tool
│   └── Program.cs                   # DLL analyzer implementation
│
├── tests/                           # Test files and test runners
│   ├── commands/                    # Command-based test files
│   │   ├── smoke-test.txt
│   │   ├── basic-test.txt
│   │   └── connection-test.txt
│   └── McpTestRunner.cs             # Programmatic test suite
│
├── testers/                         # C# test applications
│   ├── Console/                     # Console test app (GH_MCP.Tester.Console)
│   │   ├── GH_MCP.Tester.Console.csproj
│   │   └── Program.cs
│   └── Wpf/                         # WPF test app (GH_MCP.Tester.Wpf)
│       ├── GH_MCP.Tester.Wpf.csproj
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── MainWindow.xaml
│       └── MainWindow.xaml.cs
│
└── docs/                            # Testing documentation
    ├── ai-testing.md                # AI testing overview
    ├── ai-testing-guide.md          # Comprehensive AI testing workflow
    └── testing-architecture.md      # Testing system architecture
```

## Testing Approaches

### 1. Automated Script Testing
Use `testing/scripts/test.ps1` to automatically build, launch Rhino, and run tests.

**Features:**
- ✅ Builds C# code
- ✅ Launches Rhino + Grasshopper
- ✅ Opens test file (`gh_scripts/gh_mcp_load.gh`)
- ✅ No file copying required

**See:** [scripts/README.md](scripts/README.md)

### 2. Console Tester
Interactive REPL or batch mode testing via named pipe connection to Grasshopper.

**Usage:**
```powershell
cd testing\testers\Console
dotnet run
```

**Commands:** add, list, wire, search, params, export, assert_count, etc.

### 3. WPF Tester
GUI application for visual testing and debugging.

**Usage:**
```powershell
cd testing\testers\Wpf
dotnet run
```

### 4. Programmatic Tests
Comprehensive test suite in `tests/McpTestRunner.cs` covering all MCP operations.

## Documentation

### Quick References
- [TESTING.md](TESTING.md) - Main testing guide
- [QUICKSTART-TESTING.md](QUICKSTART-TESTING.md) - Quick command reference
- [AI-TESTING-QUICKSTART.md](AI-TESTING-QUICKSTART.md) - AI-driven testing quick start

### Comprehensive Guides
- [docs/ai-testing-guide.md](docs/ai-testing-guide.md) - Complete AI testing workflow
- [docs/testing-architecture.md](docs/testing-architecture.md) - Testing system design
- [docs/ai-testing.md](docs/ai-testing.md) - AI testing overview

### Script Documentation
- [scripts/README.md](scripts/README.md) - All script options and parameters

## For AI Agents

The testing infrastructure is designed for AI-driven automated testing workflows:

**Typical workflow:**
```powershell
# 1. Edit code (AI makes changes)

# 2. Test
.\testing\scripts\test.ps1

# 3. Inspect results in Grasshopper canvas

# 4. Iterate
```

**Key features for AI:**
- ✅ Exit codes for success/failure detection
- ✅ Structured JSON results in `logs/`
- ✅ Detailed log files for debugging
- ✅ Command-based test files for repeatability
- ✅ Programmatic test suite for comprehensive coverage

See [AI-TESTING-QUICKSTART.md](AI-TESTING-QUICKSTART.md) for complete AI workflow.

## Test Output Locations

Build and test logs are stored in the repository root:
```
logs/ (git-ignored)
├── dotnet-build.binlog          # Build logs
├── test-*.log                   # Test execution logs
└── test-results-*.json          # Structured test results
```

Runtime logs are written by the plugin to:
```
GH_MCP/GH_MCP/bin/{Configuration}/net48/
├── script-commands.log                       # Script component command logs
├── csharp-compilation.log                    # Compilation attempt logs
└── script-investigation-{timestamp}-{id}.txt # Structure investigation reports
```

## Testing Checklist

- [ ] Build succeeds: `cd GH_MCP; dotnet build -c Release`
- [ ] Plugin loads in Grasshopper
- [ ] Script test passes: `.\testing\scripts\test.ps1`
- [ ] Console tester connects successfully
- [ ] All programmatic tests pass
- [ ] MCP commands execute correctly

## Integration with Main Documentation

This testing directory is referenced from:
- [../CLAUDE.md](../CLAUDE.md) - Project AI instructions
- [../docs/dev-quickstart.md](../docs/dev-quickstart.md) - Development guide
- [../docs/roadmap.md](../docs/roadmap.md) - Project roadmap

## Contributing

When adding new tests:
1. Add test cases to `tests/commands/` for command-based tests
2. Update `tests/McpTestRunner.cs` for programmatic tests
3. Document new test scenarios in relevant documentation
4. Ensure tests are idempotent and can run repeatedly

---

For questions or issues with testing, see the comprehensive guides in `docs/` or check the main project [README.md](../README.md).
