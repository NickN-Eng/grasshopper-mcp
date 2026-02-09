# Grasshopper MCP - Task List

## Implemented (verified in repo)
- [x] C# MCP bridge server using stdio transport (`GH_MCP/GH_MCP_Bridge` with ModelContextProtocol).
- [x] Tools migrated to C# bridge (add_component, connect_components, clear/load/save, get_* , create_pattern).
- [x] Resources migrated to C# bridge (status, component_guide, component_library).
- [x] Component name normalization + multi-input routing in C# (`GH_MCP.Client/ComponentNormalizer.cs`, `GrasshopperTools.ConnectComponents`).
- [x] Test harness stack: `GH_MCP.Client`, console tester, WPF tester, `testing/tests/commands`, `testing/tests/McpTestRunner.cs`, `testing/docs/ai-testing.md`.

---

## Immediate gaps / blockers
- [ ] Confirm target frameworks for GH_MCP plugin (currently net48 + net7.0 + net7.0-windows in `GH_MCP/GH_MCP.csproj`).
- [ ] Implement missing GH_MCP plugin commands to match bridge/client tool surface:
- [ ] get_all_components command handler.
- [ ] get_connections command handler.
- [ ] search_components command handler.
- [ ] get_component_parameters command handler.
- [ ] validate_connection command handler.
- [ ] Re-enable `save_document` and `load_document` in `DocumentCommandHandler` (currently disabled).
- [ ] Align tool list + schemas in `docs/mcp.md` with actual bridge/client/plugin capabilities.

---

## Workstream 1: C# MCP Server Migration (remaining)
- [ ] Create deployment/installation guide for the C# bridge (build/publish, env vars, logs).
- [ ] Decide default bridge (Python vs C#) and update README/dev-quickstart.
- [ ] Deprecate/remove `grasshopper_mcp/bridge.py` once C# bridge is the default.

---

## Workstream 2: C# Script Component Creation (Rhino 8 API)
- [ ] Research Rhino 8 Script Editor API documentation.
- [ ] Investigate `GH_Component_CSNET_Script` class.
- [ ] Understand code property access (get/set source code).
- [ ] Implement compile-and-check workflow.
- [ ] Extract compilation errors with line numbers.
- [ ] Create MCP tool: `create_script_component`.
- [ ] Create MCP tool: `update_script_code`.
- [ ] Create MCP tool: `compile_script`.
- [ ] Enable iterative code refinement loop.

---

## Workstream 3: Read C# Scripts from Documents
- [ ] Identify script component types in documents.
- [ ] Extract source code from script components.
- [ ] Parse script metadata (inputs, outputs, usings).
- [ ] Create MCP tool: `get_script_components`.
- [ ] Create MCP tool: `get_script_code`.
- [ ] Handle Python/VB script components.

---

## Workstream 4: Component I/O Inspection (enhancements)
- [x] Basic input/output metadata in `get_component_info` (name, nickname, description, type, typeName).
- [ ] Expose parameter access (item/list/tree).
- [ ] Include optional vs required flags.
- [ ] Include parameter hints and descriptions consistently.
- [ ] Show current values where applicable (beyond sliders/panels).
- [ ] Expose default values.

---

## Workstream 5: Component Library via Reflection
- [ ] Enumerate all loaded `IGH_Component` types at runtime.
- [ ] Extract metadata (name, nickname, category, subcategory, GUID, I/O).
- [ ] Build searchable index and fuzzy search across all components.
- [ ] Cache catalog and auto-refresh on plugin load/unload.
- [ ] Include third-party plugin components.
- [ ] Expose MCP resource: `grasshopper://full_component_library`.
- [ ] Add MCP tools: `search_all_components`, `get_component_signature`.

---

## Workstream 6: Test Harness (done)
- [x] `GH_MCP.Client` shared library with typed command methods.
- [x] Console tester with REPL.
- [x] Batch/pipe mode for console tester.
- [x] WPF tester app.
- [x] Test command files (`testing/tests/commands`).
- [x] Documented AI testing workflow (`testing/docs/ai-testing.md`).

---

## Notes

See `docs/roadmap.md` for detailed specifications.
