# CS1 networks (roads, rails, paths)

## Data model

- **Node:** position, flags (junction, bend, end, middle, traffic lights, outside connection, underground, ...), elevation, up to 8 connected segments, and an optional node building (e.g. pillars).
- **Segment:** start and end node, start and end direction (tangents), flags, a `NetInfo` prefab and lanes. The centre line is a cubic Bézier built from the node positions and directions.
- **Lane:** a `NetLane` with its own world-space Bézier (`m_bezier` [verified]), length and flags. Its `NetInfo.Lane` gives the position offset, width, direction, lane type, vehicle types and speed limit (`m_speedLimit` [verified]).
- **NetInfo (prefab):**
  - geometry: half width (`m_halfWidth`), pavement width (`m_pavementWidth`), segment length (`m_segmentLength`) [verified names];
  - content: lanes, segment meshes, node meshes, lane props;
  - behaviour: an AI (road, bridge, tunnel, slope, rail, pedestrian, ...) and elevation variants.
- **Speed units:** 1.0 = 50 km/h. [community: TM:PE convention; verify]

## Mesh conventions

[community: [Road Editor wiki](https://skylines.paradoxwikis.com/Road_Editor), [cslmodding network page](https://cslmodding.info/asset/network/)]

- Segment meshes are straight stubs, usually 64 m long along Z and centred on the origin. X spans ±halfWidth and Y is up. They are usually subdivided about 16 times along Z, and `NetInfo.m_segmentLength` matches the mesh length.
- Pavement is 0.3 m high with its top at y = 0, so the road surface sits at about −0.3 m.
- Junction node meshes have a cut through the middle; the centre vertices move to the middle of the intersection.
- The V texture coordinate tiles about every 20 m. Vertex colour green = 0 disables that tiling.

## How the game renders them

The GPU bends the straight meshes: C# computes per-instance shader inputs, and the vertex shader does the deformation.

- **Shader properties** [verified 2026-11-16: names present in Assembly-CSharp]:
  - `_LeftMatrix`, `_RightMatrix`, `_LeftMatrixB`, `_RightMatrixB`;
  - `_MeshScale`, `_CenterPos`, `_SideScale`, `_ObjectIndex`;
  - array variants `_MeshScales`, `_CenterPositions`, `_SideScales`.
- **Segments** (`NetSegment.RenderInstance`) [community: decompiled in [NodeController NetSegment2.cs](https://github.com/kianzarrin/NodeController/blob/master/NodeController/DecompiledSources/NetSegment2.cs)]:
  - The instance position is the midpoint of the two node positions, with identity rotation.
  - `CalculateCorner` gives four corners (start/end × left/right), and `CalculateMiddlePoints` gives the inner control points [verified names].
  - `_LeftMatrix` and `_RightMatrix` come from `CalculateControlMatrix(...)` over those points and the AI's V scale.
  - `_MeshScale = (0.5/halfWidth, 1/segmentLength, 1, 1)`, with x and y negated when the segment mesh is used turned around.
- **Control matrix layout** [community: [AdaptiveRoads TrackRenderData.cs](https://github.com/kianzarrin/AdaptiveRoads/blob/master/AdaptiveRoads/Data/NetworkExtensions/TrackRenderData.cs)]:
  - Columns 0–3 are the Bézier control points a, b, c, d, relative to the instance position.
  - Rows 0–2 are x, y, z.
  - Row 3 holds a texture V coordinate per control point, scaled by the V scale (0.05 ≈ 20 m tiling).
- **Nodes** [community: [NetworkSkins2 NetNodeRenderInstancePatch.txt](https://github.com/boformer/NetworkSkins2/blob/HEAD/NetworkSkins/Patches/NetNode/NetNodeRenderInstancePatch.txt)]:
  - Junction and end meshes take all eight properties (`_LeftMatrix`, `_RightMatrix`, `_LeftMatrixB`, `_RightMatrixB`, `_MeshScale`, `_CenterPos`, `_SideScale`, `_ObjectIndex`) from the render instance data computed in `NetNode.RefreshJunctionData` and `RefreshEndData` [verified names].
  - Bend nodes redraw the segment mesh using only `_LeftMatrix` and `_RightMatrix` (from `RefreshBendData` [verified name]).
  - Direct-connect meshes also use only the A matrices.
- **`RefreshJunctionData`** [community: [AdaptiveRoads RefreshJunctionData.cupcake.cs](https://github.com/kianzarrin/AdaptiveRoads/blob/master/RefreshJunctionData.cupcake.cs), [NodeController NetNode.cs](https://github.com/kianzarrin/NodeController/blob/master/NodeController/DecompiledSources/NetNode.cs)]:
  - It chooses one neighbour segment on each side, using dot and cross products of the segment directions.
  - It builds four Béziers from this segment's corners to the neighbours' corners. The middle-point ratio is 0.3; newer versions scale it by pavement ratios, clamped to 0.05–0.7.
  - It stores the mesh scale (including pavement ratios), the centre position relative to the node, and the side scales.
  - At dead ends, control points are offset by end radius × 4/3.

## Reproducing the geometry on the CPU

Status: open; step 0.7 investigates.

- No public CPU reimplementation of the shader exists. Mods call the vanilla matrix code and let the GPU do the bending. [community]
- The game builds far-LOD group meshes on the CPU, via static `NetSegment` and `NetNode` methods `PopulateGroupData` and `CalculateGroupData`, using `m_combinedLod` and `RenderGroup.MeshData` [verified names]. [inferred] These methods must contain a C# version of the bending math. They are the reference to validate our implementation against, or to call directly.
- Presumed segment formula [inferred, unverified]:
  - For a mesh vertex (x, y, z), let s = x · MeshScale.x + 0.5 and t = z · MeshScale.y + 0.5.
  - Then position = lerp(B_left(t), B_right(t), s) + y · up, where B_left and B_right are the cubic Béziers stored in the left and right control matrices.
- The junction formula, which uses `_CenterPos` and `_SideScale`, is unknown.

## Lane props

[community: [NodeController NetLane2.cs](https://github.com/kianzarrin/NodeController/blob/master/NodeController/DecompiledSources/NetLane2.cs), [AdaptiveRoads PropRenderData.cs](https://github.com/kianzarrin/AdaptiveRoads/blob/master/AdaptiveRoads/Data/NetworkExtensions/PropRenderData.cs)]

- **Count along a lane:** 2 · max(1, round(length / repeatDistance)) when the repeat distance is greater than 1; otherwise 2.
- **Positions:** t = segmentOffset/2 (+ pos.z/length, clamped and mirrored on reversed lanes) + j/count, for odd j.
- **Randomness:** each lane prop uses a `Randomizer(laneID + propIndex)` for the probability roll, variation, scale and colour. Traffic lights take their colour from the node state.
- **Transform:**
  - lateral offset along the lane normal;
  - angle from the normal, plus corner blending, plus the prop's own angle;
  - height = lane Bézier height + pos.y.
- **Visibility** depends on the lane flags and the start and end node flags (`CheckFlags`).
- In the exporter, prefer calling the game's own logic, or capturing its results, over reimplementing this.

## Open questions

- The exact segment and junction deformation (step 0.7).
- Whether vanilla network meshes are readable from C# (`Mesh.isReadable`).
- Speed-limit units (verify 1.0 = 50 km/h).
