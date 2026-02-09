# Development Roadmap

## Workstream 1: C# MCP Server Migration

Goal: Replace Python bridge with native C# MCP server for easier deployment and tighter integration.

Rationale
- Single deployment artifact (no Python runtime required)
- Direct .NET integration with Grasshopper
- Better performance (no socket overhead)
- Simplified user installation

Tasks
- [ ] Research C# MCP SDK options (e.g., ModelContextProtocol NuGet package)
- [ ] Design server architecture (stdio vs SSE transport)
- [ ] Migrate tools from Python to C#
- [ ] Migrate resources from Python to C#
- [ ] Implement component name normalization in C#
- [ ] Add connection intelligence logic
- [ ] Create deployment/installation guide
- [ ] Deprecate Python bridge

Files to create/modify
- GH_MCP_Bridge/ (new MCP server project)
- GH_MCP/ (refactor to library for shared logic)

---

## Workstream 2: C# Script Component Creation (Rhino 8 API)

Goal: Enable Claude to create, edit, and iterate on C# script components within Grasshopper.

Research areas
- Rhino 8 ScriptEditor API
- GH_ScriptComponent class and instantiation
- Code compilation within Grasshopper
- Error extraction and feedback
- Test execution within script context

Tasks
- [ ] Research Rhino 8 Script Editor API documentation
- [ ] Investigate GH_Component_CSNET_Script class
- [ ] Understand code property access (get/set source code)
- [ ] Implement compile-and-check workflow
- [ ] Extract compilation errors with line numbers
- [ ] Create MCP tool: create_script_component(code, inputs, outputs)
- [ ] Create MCP tool: update_script_code(component_id, code)
- [ ] Create MCP tool: compile_script(component_id) -> returns errors
- [ ] Enable iterative code refinement loop

New MCP tools needed
```
create_script_component(code, inputs, outputs, x, y)
update_script_code(component_id, new_code)
get_script_code(component_id)
compile_script(component_id) -> {success, errors[]}
run_script_tests(component_id, test_cases)
```

---

## Workstream 3: Read C# Scripts from Grasshopper Documents

Goal: Allow Claude to read existing C# script component code from Grasshopper files.

Tasks
- [ ] Identify script component types in documents
- [ ] Extract source code from script components
- [ ] Parse script metadata (inputs, outputs, using statements)
- [ ] Create MCP tool: get_script_components() -> list all scripts
- [ ] Create MCP tool: get_script_code(component_id) -> source code
- [ ] Handle Python/VB script components if present

New MCP tools needed
```
get_script_components() -> [{id, name, language, inputs, outputs}]
get_script_code(component_id) -> {code, language, inputs, outputs}
```

---

## Workstream 4: Component I/O Inspection

Goal: Enable Claude to deeply inspect component inputs/outputs for intelligent automation.

Current state
- get_component_info returns basic parameter info
- Limited type information available

Enhancements needed
- [ ] Expose parameter data types (Point3d, Curve, Number, etc.)
- [ ] Show parameter access (item, list, tree)
- [ ] Include parameter hints and descriptions
- [ ] Show current values where applicable
- [ ] Expose optional vs required status
- [ ] Show default values

Enhanced MCP response structure
```json
{
  "inputs": [{
    "name": "Points",
    "nickname": "P",
    "type": "Point3d",
    "access": "list",
    "optional": false,
    "description": "Points to process",
    "currentValue": [...],
    "typeName": "Grasshopper.Kernel.Types.GH_Point"
  }],
  "outputs": []
}
```

---

## Workstream 5: Component Library via Reflection

Goal: Build comprehensive component catalog using runtime reflection of loaded Grasshopper plugins.

Current state
- ComponentKnowledgeBase.json has ~20 manually documented components
- Grasshopper has 500+ built-in components
- Third-party plugins not cataloged

Tasks
- [ ] Enumerate all loaded IGH_Component types at runtime
- [ ] Extract component metadata via reflection:
  - Name, nickname, category, subcategory
  - GUID for reliable instantiation
  - Input/output parameter definitions
  - Icon (base64 encoded)
- [ ] Build searchable index
- [ ] Implement fuzzy search across all components
- [ ] Cache catalog for performance
- [ ] Auto-refresh on plugin load/unload
- [ ] Include third-party plugin components

New MCP tools/resources
```
grasshopper://full_component_library -> {categories, components[]}
search_all_components(query, category?) -> matches with relevance
get_component_signature(guid) -> full parameter specification
```

Implementation approach (sketch)
```csharp
// Enumerate all components
foreach (var proxy in Grasshopper.Instances.ComponentServer.ObjectProxies)
{
    if (proxy.Kind == GH_ObjectType.CompiledObject)
    {
        // Extract metadata
        var component = proxy.CreateInstance() as IGH_Component;
        // Build catalog entry
    }
}
```

---

## Workstream 6: Test Harness for GH_MCP Plugin

Goal: Create standalone apps to test the GH_MCP TCP server without the full MCP bridge or Claude Desktop.

### Option A: Console App (Recommended First)

**Pros:**
- Fast to build
- Scriptable for automated testing
- Can pipe JSON files for batch testing
- CI/CD friendly

**Features:**
```
GH_MCP_Tester.exe

Interactive Commands:
  connect                     - Connect to GH_MCP on localhost:8080
  send <json>                 - Send raw JSON command
  add <type> <x> <y>          - Shorthand for add_component
  list                        - List all components (get_all_components)
  info <id>                   - Get component info
  wire <src> <srcParam> <tgt> <tgtParam>  - Connect components
  clear                       - Clear document
  script                      - Enter multi-line JSON mode
  load <file.json>            - Load and execute commands from file
  history                     - Show command history
  exit                        - Quit

Batch Mode:
  GH_MCP_Tester.exe < commands.txt
  type test.json | GH_MCP_Tester.exe --json
```

### Option B: WPF App (For Interactive Exploration)

**Pros:**
- Visual command builder with dropdowns
- Pretty-printed JSON request/response viewer
- Command history with replay
- Parameter templates for each command type
- Connection status indicator

**Features:**
- Command type dropdown (add_component, connect_components, etc.)
- Dynamic parameter form based on command type
- Split view: Request JSON | Response JSON
- Syntax highlighting
- Save/load command sessions
- Auto-complete for component types

### Option C: Shared Client Library

Build a `GH_MCP.Client` library used by both apps:
```csharp
public class GhMcpClient
{
    public async Task<Response> SendCommandAsync(Command cmd);
    public async Task<Response> AddComponentAsync(string type, int x, int y);
    public async Task<Response> ConnectAsync(string src, string srcParam, string tgt, string tgtParam);
    public async Task<List<ComponentInfo>> GetAllComponentsAsync();
    // etc.
}
```

### Tasks
- [ ] Create GH_MCP.Client library with typed command methods
- [ ] Build console app with interactive REPL
- [ ] Add batch/pipe mode to console app
- [ ] Build WPF app with command builder UI
- [ ] Create test command files for common scenarios
- [ ] Document test workflows

### Files to create
- `GH_MCP/GH_MCP.Client/` - Shared client library
- `testing/testers/Console/` - Console test app
- `testing/testers/Wpf/` - WPF test app (optional)
- `testing/tests/commands/` - JSON test command files
