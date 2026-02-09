# Script Component Diagnosis and Recommended Approaches

## Problem Discovery

After implementing comprehensive logging in `CSharpScriptCompiler_OptionA.cs`, the root cause was identified:

**The `Context` property does not exist on `RhinoCodePluginGH.Components.CSharpComponent`**

From `csharp-compilation.log`:
```
[2026-02-06 19:45:14.750] [OptionA] GetPropertyValue: Property 'Context' not found on RhinoCodePluginGH.Components.CSharpComponent
[2026-02-06 19:45:14.751] [OptionA] ERROR: Context is null
```

This means the implementation plan documents that suggested accessing the `Context` property were based on incorrect assumptions about the new RhinoCode script components.

## Recommended Approach: Script Investigator Component ✅ IMPLEMENTED

**Status: Ready to use**

Created a new Grasshopper component `Script Investigator` that uses reflection to discover the actual structure of script components.

### How to Use:

1. **Place component on canvas**: Find "Script Investigator" in `Params > Util` tab
2. **Connect script component**: Wire any script component (C#, Python, etc.) to the input
3. **Read report**: The component outputs:
   - Report text (can view in panel)
   - Report file path (saved to GHA folder)
   - Success boolean

### What it Discovers:

- All properties on the component object
- All methods on the component object
- Nested objects (CodeEditor, ScriptInstance, Document, Code, Script, Editor, etc.)
- Text properties containing source code
- Compilation methods

### Report Location:

Reports are saved to: `{GHA-folder}/script-investigation-{timestamp}-{id}.txt`

Example: `GH_MCP\GH_MCP\bin\Debug\net48\script-investigation-20260206-143022-04acb929.txt`

### Component Details:

- **Name**: Script Investigator
- **Nickname**: ScriptInv
- **Category**: Params
- **Subcategory**: Util
- **Input**: Component (C) - Script component to investigate
- **Outputs**:
  - Report (R) - Full investigation report text
  - Report File (F) - Path to saved report file
  - Success (S) - True if investigation succeeded

**Files created:**
- `GH_MCP/GH_MCP/GH_ScriptInvestigatorComponent.cs` - New Grasshopper component
- `GH_MCP/GH_MCP/Utils/ReflectionInvestigator.cs` - Updated to discover without assumptions

**Alternative: MCP Tool** (also available but component is preferred)
- Command: `investigate_script_component(component_id)`
- Requires getting component ID first via `get_script_components()`

### Approach 2: Use Option B (DLL Reference)

**Status: Not recommended**

Directly reference RhinoCodePluginGH.dll and use compile-time types. This is fragile across Rhino/Grasshopper versions and defeats the purpose of the reflection-based approach.

### Approach 3: Try Alternative Property Names

**Status: Built into Approach 1**

The updated ReflectionInvestigator now checks these property names automatically:
- `Context`
- `CodeEditor`
- `ScriptInstance`
- `Document`
- `Code`
- `Script`
- `Editor`

## Next Steps

1. **Run investigation tool** on a real C# script component:
   ```
   investigate_script_component(component_id="04acb929-e7e8-4edf-8865-385f1d02fa57")
   ```

2. **Read the investigation report** to discover:
   - What properties actually exist
   - Where the script text is stored
   - What methods can trigger compilation
   - The actual object hierarchy

3. **Update CSharpScriptCompiler_OptionA.cs** based on discoveries:
   - Replace `Context` property access with actual property name
   - Update method discovery logic
   - Update script text get/set logic

4. **Test the updated implementation**:
   - `get_script_code` should retrieve script text
   - `set_script_code` should set script text
   - `compile_script` should trigger compilation

## Implementation Status

### Completed ✅
1. Fixed non-blocking UI thread calls in Option A (using ManualResetEventSlim)
2. Added comprehensive logging throughout all methods
3. Added proper error collection and diagnostics
4. Created `investigate_script_component` tool for discovery
5. All code builds successfully

### Pending ⏳
1. Run investigation tool on actual component
2. Read investigation report
3. Update Option A with correct property/method names
4. Test get/set/compile commands

## Log Files

All operations are logged for debugging:

- **Script commands log**: `GH_MCP\GH_MCP\bin\Debug\net48\script-commands.log`
  - High-level command execution (get, set, compile)

- **Compilation log**: `GH_MCP\GH_MCP\bin\Debug\net48\csharp-compilation.log`
  - Detailed reflection attempts (Option A)
  - Property access attempts
  - Method discovery attempts

- **Investigation reports**: `GH_MCP\GH_MCP\bin\Debug\net48\investigation-{component_id}.txt`
  - Full component structure
  - All properties and methods discovered
  - Nested object hierarchies

## Example Workflow

### Using the Grasshopper Component (Recommended):

```
1. Open Grasshopper
2. Add a C# script component to canvas
3. Place "Script Investigator" component (Params > Util)
4. Connect C# script component output to Script Investigator input
5. The component will automatically investigate and save a report
6. Read the report file path from the "Report File" output
7. Open the report file to see the complete structure
```

### Using MCP Tools (Alternative):

```bash
# 1. Get list of script components
get_script_components()
# Returns: [{ "id": "04acb929-...", "name": "C# Script", ... }]

# 2. Investigate the component structure
investigate_script_component(component_id="04acb929-e7e8-4edf-8865-385f1d02fa57")
# Returns: { "report_file": "...\investigation-04acb929-....txt", ... }

# 3. Read the investigation report
Read(file_path="GH_MCP\GH_MCP\bin\Debug\net48\investigation-04acb929-....txt")
```

### After Investigation:

```bash
# 4. Look for in the report:
#    - Properties that might contain script text (Code, Text, SourceCode, etc.)
#    - Methods that might trigger compilation (Compile, Build, Execute, etc.)
#    - Objects that might contain these (CodeEditor, ScriptInstance, etc.)

# 5. Update CSharpScriptCompiler_OptionA.cs with correct names

# 6. Rebuild and test
dotnet build -c Release
get_script_code(component_id="...")
set_script_code(component_id="...", code="// test")
compile_script(component_id="...")
```
