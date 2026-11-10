# P3 gateway repair and official Blender MCP setup

Date: 2026-11-10. Status: Concluded for passage obstruction.

## Hand-off brief
The Meshy FBX has distorted internal geometry partially obstructing the doorway. The official Blender Lab MCP bridge is installed and configured for Codex. A repaired copy clears the passage; the original asset, generated surface character and overall dimensions are preserved.

## Evidence and findings
- Confirmed: installed Blender add-on is lab_blender_org/mcp 1.0.3; Blender reports 5.2.3 LTS; bridge listens on localhost:9876.
- Confirmed: source is Assets/_Game/Art/Artworks/Ruins/Concepts/P3 slot gateway.fbx, not the Environment path initially supplied.
- Confirmed: original has 45,189 vertices, 90,630 polygons, and no nonmanifold edges. Front/back renders and ray queries show an upper passage obstruction. A closed mesh alone does not establish an unobstructed passage.
- Hypothesis: image-to-3D inferred an incorrect interior from the single reference. Unconfirmed generation cause; generated mesh defect is directly observed.
- Confirmed: source has no material slots or supplied textures in the imported FBX.

## Repair
Used execute_blender_code through the official MCP server and Python MCP ClientSession over stdio. Installed bridge source from https://projects.blender.org/lab/blender_mcp.git under C:/Users/valentin/.local/share/blender-mcp-official. Codex config registers uv run --project .../mcp blender-mcp under mcp_servers.blender. No community blender-mcp PyPI package was substituted.

On a duplicate, subtracted an extruded passage cutter with parallel sides, flat head and small rounded top corners. Cutter spans the entire depth and slightly widens the opening to remove obstructions. FLOAT solver retained the shell; EXACT produced an invalid silhouette and was rejected by visual/dimension checks. A trial interior fill produced nonmanifold edges and was rejected. Original source is unchanged.

## Verification
Final FBX was exported and reimported: 70 rays through the passage in both directions are clear; zero nonmanifold edges. Front, back and three-quarter renders inspected. Final geometry: 35,675 vertices, 69,282 polygons. Overall dimensions unchanged: 1.271438 x 0.535981 x 1.534275 in current imported units (X/Y/Z). Validation scripts and before/after renders are in ignored Temp/gateway_* files.

## Deliverables
Alongside source: P3 slot gateway repaired.fbx and P3 slot gateway repaired.blend. Blender asset scene contains the repaired object selected. The blend also retains the original and inspection scene for comparison.

## Limits and next steps
This fixes the blocked passage, not the entire look-test quality bar. Generated faceting, uneven grooves and interior surface irregularities remain. Scale and proportions still reflect Meshy output rather than the design's metric dimensions and 1:7 opening. No materials were invented and no Unity scene/prefab import was performed. Restart/reload Codex to expose the registered Blender tools natively; this session used the official bridge through a local MCP client.

## Follow-up: 2026-11-10 - circular cavity inside passage

User identified an internal hollow matching the outer circle. Confirmed in the previous three-quarter inspection: the first repair cleared the central opening but retained a cavity extending sideways into the stone. Earlier verification did not test lateral passage wall continuity.

Filled the hidden interior with a solid core, voxel-remeshed the union at 0.003 imported units, recut the rounded-corner slot, and decimated the result. This slightly softens the existing surface while retaining the exterior circle as a shallow groove. Direct boolean unions of the original generated topology were rejected due to missing shell or nonmanifold geometry.

Final: 33,566 vertices / 55,219 polygons. Zero nonmanifold edges. 55 interior checks confirm continuous parallel side walls and ceiling, with maximum side-wall deviation approximately 0.000011 imported units. Reexported and reimported FBX passes the 70 clear passage-ray checks. Before/after silhouette dimensions agree within 0.0001 imported units. Updated the same repaired FBX and blend files and preview; original source remains untouched. This follow-up supersedes the earlier note that interior irregularities remain.
