# Grasshopper MCP Tool Capabilities

This guide describes what the current tool can do today, based on the C# plugin in `GH_MCP/GH_MCP` and the Python MCP bridge in `grasshopper_mcp/bridge.py`.

## Runtime pieces
- Grasshopper plugin (`GH_MCPComponent.cs`) hosts a TCP server on localhost (default port 8080). The server runs only while the Grasshopper MCP component is on the canvas and `Enabled` is true.
- Python bridge (`grasshopper_mcp/bridge.py`) exposes MCP tools and resources, and forwards commands to the C# plugin.

## C# command API (registered in `GrasshopperCommandRegistry`)

### Geometry commands
- `create_point`
  - Params: `x`, `y`, `z` (numbers)
  - Returns: `{ id, x, y, z }`
  - Notes: Creates a `Rhino.Geometry.Point3d` only; it does not add a Grasshopper component to the document.
- `create_curve`
  - Params: `points` (array of `{x, y, z}` objects)
  - Returns: `{ id, pointCount, length }`
  - Notes: Uses a line for 2 points; otherwise creates an interpolated curve.
- `create_circle`
  - Params: `center` (`{x, y, z}`), `radius`
  - Returns: `{ id, center, radius, circumference }`

### Component commands
- `add_component`
  - Params: `type`, `x`, `y`
  - Behavior:
    - Normalizes component names via `FuzzyMatcher.GetClosestComponentName`.
    - Explicitly supports: `XY Plane`, `XZ Plane`, `YZ Plane`, `Plane 3Pt`, `Box`, `Sphere`, `Cylinder`, `Cone`, `Circle`, `Rectangle`, `Line`, `Construct Point`, `Panel`, `Number Slider`, and parameter components (`Point`, `Curve`, `Circle`, `Line`, `Number`).
    - Falls back to lookup by GUID, name, or partial name.
    - Adds the component to the active document and sets its pivot to `(x, y)`.
  - Returns: `{ id, type, name, x, y }`
- `connect_components`
  - Params: `sourceId`, `targetId`, and either `sourceParam` or `sourceParamIndex`, plus either `targetParam` or `targetParamIndex`.
  - Behavior:
    - Normalizes parameter names via `FuzzyMatcher.GetClosestParameterName`.
    - Resolves parameters by name, nickname, or index.
    - Removes existing sources on the target input before connecting.
    - Performs compatibility checks, including numeric-to-numeric, curve/geometry, point/vector, plane-to-geometry, and a guard against connecting Number Slider to the Circle parameter container.
  - Returns: connection metadata on success.
- `set_component_value`
  - Params: `id`, `value` (string)
  - Behavior:
    - Sets `GH_Panel.UserText` when the component is a panel.
    - Sets `GH_NumberSlider` value when the component is a slider.
    - Otherwise tries to set the first input parameter if it is `Param_String` or `Param_Number`.
  - Returns: `{ id, type, value }`
- `get_component_info`
  - Params: `id`
  - Returns: basic metadata plus `inputs` and `outputs` arrays; includes panel text and slider value/min/max when applicable.

### Document commands
- `get_document_info`
  - Returns document name, path, component count, and a list of `{ id, type, name }`.
- `clear_document`
  - Removes all non-essential objects while preserving MCP/Claude components, toggles, and panels.
- `save_document` and `load_document`
  - Currently disabled in C# and always return an error message.

### Intent commands (pattern creation)
- `create_pattern`
  - Params: `description`
  - Uses `IntentRecognizer` and `Resources/ComponentKnowledgeBase.json` to map keywords to patterns.
  - Currently defined patterns:
    - `3D Box`
    - `3D Voronoi`
    - `Circle`
  - Returns: `{ Pattern, ComponentCount, ConnectionCount }`
- `get_available_patterns`
  - Params: `query`
  - Returns a list containing the best match (if any). If no query is provided, it currently returns an empty list.

### Verification commands (AI testing)
- `export_document_state`
  - Returns full document state (components, inputs, outputs, slider values, panel text, and connections).
- `assert_component_exists`
  - Params: `componentId`
  - Returns pass/fail with component details.
- `assert_connection_exists`
  - Params: `sourceId`, `targetId`, optional `sourceParam`, `targetParam`
  - Returns pass/fail and the resolved connection parameters.
- `assert_component_count`
  - Params: `expected`
  - Returns pass/fail with expected vs actual counts.
- `get_document_hash`
  - Returns a SHA256 hash of the document state for quick comparisons.

## Fuzzy matching rules (C#)

Component name aliases (`FuzzyMatcher.ComponentNameMap`):
- Plane: `plane`, `xy`, `xyplane`, `xz`, `xzplane`, `yz`, `yzplane`, `plane3pt`, `3ptplane`
- Geometry: `box`, `cube`, `rect`, `rectangle`, `circle`, `circ`, `sphere`, `cylinder`, `cyl`, `cone`
- Params: `slider`, `numberslider`, `panel`, `point`, `pt`, `line`, `ln`, `curve`, `crv`

Parameter name aliases (`FuzzyMatcher.ParameterNameMap`):
- Plane-related: `plane`, `base`, `origin`
- Size-related: `radius`, `r`, `size`, `xsize`, `ysize`, `zsize`, `width`, `length`, `height`, `x`, `y`, `z`
- Point-related: `point`, `pt`, `center`, `start`, `end`
- Numeric: `number`, `num`, `value`
- Output: `result`, `output`, `geometry`, `geo`, `brep`

## Python MCP tools and resources

The Python bridge exposes the following MCP tools in `grasshopper_mcp/bridge.py`:
- `add_component`, `clear_document`, `save_document`, `load_document`, `get_document_info`
- `connect_components`, `create_pattern`, `get_available_patterns`, `get_component_info`
- `get_all_components`, `get_connections`, `search_components`, `get_component_parameters`, `validate_connection`

Notes on current behavior:
- The Python tools forward to C# command names. Only commands registered in `GrasshopperCommandRegistry` will succeed.
- `get_all_components`, `get_connections`, `search_components`, `get_component_parameters`, and `validate_connection` are not implemented in C# yet, so they currently return "no handler registered".
- The Python `get_component_info` tool sends `componentId`, while C# expects `id`. This will currently return an error unless the C# side is updated.
- `save_document` and `load_document` are implemented on the C# side but always return "temporarily disabled".

The Python bridge also exposes resources:
- `grasshopper://status` - Document summary, component hints, and connection summaries (depends on `get_all_components` and `get_connections`).
- `grasshopper://component_guide` - Static guide for common components and connections.
- `grasshopper://component_library` - Static component catalog with categories and data types.
