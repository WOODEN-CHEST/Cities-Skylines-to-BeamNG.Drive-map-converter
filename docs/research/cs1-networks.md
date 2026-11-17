# CS1 networks (roads, rails, paths)

## Data model

[verified 2026-11-17, game 1.21.1-f9: fields, enums and methods read in the IL (`monodis`, locally); behaviour checked numerically on CityPalma's dump `san-palma_20261117-214514` (2,477 nodes, 2,482 segments, 8,638 lanes, 28 prefabs), with the scratch checks of step 1.3]

- **Buffers and ids:** `NetManager.m_nodes`, `m_segments` and `m_lanes` hold the objects (vanilla lengths 32,768, 36,864 and 262,144); an object's id is its index, and id 0 is never used, so 0 means "none". Live entries have the `Created` flag (0x1). The dump stores the objects in the sections `net-nodes`, `net-segments`, `net-lanes` and their prefabs in `net-prefabs` ([dump format](../dump-format.md#networks)).
- **Node (`NetNode`):**
  - `m_position`; `m_flags` (`NetNode.Flags`, 32 bits: 0x1 created, 0x10 end, 0x20 middle, 0x40 bend, 0x80 junction, 0x400 outside connection, 0x4000 on the ground, 0x80000 underground, 0x100000 transition, 0x200000 level crossing, 0x800000 traffic lights, 0x80000000 traffic lights switched by the player, among others); `m_flags2` (`Flags2`: bollards, road-end and event bits). The property `flags` (`FlagsLong`) combines both, with `m_flags2` in the upper 32 bits.
  - `m_segment0` to `m_segment7` (`GetSegment(i)`): eight segment slots, 0 when empty. `m_building`: the node's building, such as a pillar or pylon. `m_infoIndex`: the prefab's index in `PrefabCollection<NetInfo>`.
  - `m_heightOffset` (byte, 1/64 m): added to the corners at this node when they are computed with the height offset (Corners).
  - `m_elevation` (byte): the elevation the network tool gave the node, in metres: 12, 18 or 24 on CityPalma's elevated roads, tracks and ramps, which lie 5.3 to 24.6 m above today's ground, most within 1.5 m of the value. So it is the tool's setting at the time, not the current height above the ground.
  - CityPalma: 176 junctions, 90 bends, 56 dead ends, 2,138 middle nodes (between two segments of one road); 7 with traffic lights; 17 outside connections, at the map's edges.
- **Segment (`NetSegment`):**
  - `m_startNode`, `m_endNode`; `m_startDirection`, `m_endDirection`: the centre line's directions at the nodes, both pointing from the node into the segment.
  - `m_flags` (`NetSegment.Flags`: 0x1 created, 0x10 invert, others for traffic, crossings, stops, bans and names); `m_flags2`; `m_infoIndex`; `m_lanes`, the first lane of its lane list.
  - `m_middlePosition`: the average of the midpoints of the two edge curves (`UpdateBounds`). `m_averageLength`: the mean of its lanes' `m_length` (`NetAI.UpdateLanes`); in CityPalma within 15 µm for every segment except one bus line's.
  - **Centre curve:** the game stores none; where it needs one (for example `GetClosestPositionAndDirection`) it builds a cubic Bézier from node to node: a and d are the nodes' positions, b and c come from `NetSegment.CalculateMiddlePoints(a, m_startDirection, d, m_endDirection, smoothStart, smoothEnd)`, smooth at an end whose node is a middle node. The exporter calls these methods the same way.
- **Lane (`NetLane`):** `m_bezier` (the lane's curve), `m_length`, `m_curve`, `m_segment`, `m_flags` (16 bits: 0x1 created, 0x4 inverted, 0x10/0x20/0x40 the lane's arrows: straight on, left, right; 0x400/0x800 yield), `m_nextLane`. A segment's lanes form a list, `m_lanes` then `m_nextLane`, in the order of its prefab's `NetInfo.m_lanes`, so a lane's place in the list is its index in the prefab (Lanes).
- **Prefab (`NetInfo`):** dimensions (`m_halfWidth`, `m_pavementWidth`, `m_segmentLength`, `m_surfaceLevel`), terrain flags (`m_createPavement`, `m_createGravel`, `m_createRuining`, `m_flattenTerrain`, `m_lowerTerrain`, `m_clipTerrain`, `m_followTerrain`, `m_flatJunctions`), the item class `m_class`, the lanes `m_lanes`, the AI `m_netAI`, meshes and lane props (Prefabs).
- **Speed units:** `NetInfo.Lane.m_speedLimit` is in units of 50 km/h. [verified: the road tooltip (`RoadBaseAI.GetLocalizedTooltip`, text `AIINFO_SPEED`) shows round(10 · `m_averageVehicleLaneSpeed`) · 5, where `InitializePrefab` makes `m_averageVehicleLaneSpeed` the mean limit of the lanes for cars, trams and trolleybuses (vehicle type & 0x10041): the limit times 50. The unit's name is in the game's encoded locale files, which weren't read; km/h is the convention of mods such as TM:PE [community].] CityPalma: the two-lane road 0.8 (40 km/h), the four-lane road 1 (50), the highway 2 (100), the highway ramp 1.6 (80), train tracks 3 (150), pedestrian lanes 0.25.

### Corners

[verified 2026-11-17: IL of `NetSegment.CalculateCorner` and its callers; CityPalma's dump]

- `segment.CalculateCorner(segmentID, heightOffset, start, leftSide, out position, out direction, out smooth)` gives the corner where one edge of the segment's surface meets the start or end node, and the edge's direction there, pointing into the segment. It calls a static overload with the prefab, the node positions and directions.
- **Sides:** `leftSide` is seen from the corner's own node, looking into the segment. The left offset is the normalised cross product of the direction with up, (−dZ, 0, dX), times `m_halfWidth`; the right one its negative. Seen from above with +X east and +Z north, this is the left of the direction: west when heading north. So the start's left corner and the end's right corner lie on the same side of the segment.
- **Height:** the corner lies at its node's height; with `heightOffset` the node's `m_heightOffset` / 64 m is added. The renderer (How the game renders them) and `NetAI.UpdateLanes` use `heightOffset` true, and so does the exporter.
- **Position:** at a middle node or a dead end the corner lies `m_halfWidth` across the (smoothed) direction from the node. At junctions and bends the game moves it back along the edge curve, by at least the AI's minimum corner offset, to clear the other segments. CityPalma: the two corners at an end are exactly twice `m_halfWidth` apart at 4,276 middle-node ends and 56 dead ends (within 0.01 %), but 0.99 to 1.48 times at junctions and up to 1.33 times at bends.
- `smooth` is whether the corner's node is a middle node; `UpdateLanes` passes the right-side calls' values on to `CalculateMiddlePoints`.

### Lanes

[verified 2026-11-17: IL of `NetAI.UpdateLanes`, `NetLane.UpdateLength`, `NetInfo.InitializePrefab` and `NetSegment.CountLanes`; CityPalma's dump]

- **Curves:** `NetAI.UpdateLanes` builds each lane from the four corners. With t = `position` / (2 · `m_halfWidth`) + 0.5, and 1 − t on an inverted segment: a = startLeft + (startRight − startLeft) · t, d = endRight + (endLeft − endRight) · t, the directions interpolated the same way; a and d are raised by the lane's `m_verticalOffset`; b and c come from `CalculateMiddlePoints` with the corners' smoothness. So a lane always runs from the segment's start to its end, and an inverted segment mirrors its lanes. Rebuilt this way from the dumped corners, all 8,638 lanes of CityPalma match the game's curves within 0.5 mm.
- **Direction:** `NetInfo.Lane.m_finalDirection` is `m_direction`, reversed for vehicle, parking, cargo and transport-vehicle lanes (lane type & 0x35) in cities that drive on the left (`InitializePrefab`). On a segment the game reverses it once more if the segment is inverted (`CountLanes`); forward is from the start node to the end node. `UpdateLanes` sets the lane flag 0x4 on every lane of a city that drives on the left.
- **Length:** `NetLane.UpdateLength` sets `m_length` to the length of the curve's control polygon, |b − a| + |c − b| + |d − c|, which exceeds the curve's own length where it bends, and `m_curve` to (π/2) · (1 − the dot product of the directions at the ends) / `m_length`. CityPalma: true for all lanes but one of a bus line (within 1 mm), whose AI sets its lengths from the line's path.

### Prefabs

[verified 2026-11-17: IL; CityPalma's 28 prefabs]

- **Item class** (`m_class`, an `ItemClass`): service, sub-service, level and layers. Roads have the service `Road`: the two-lane roads' class `Small Road` has level 2, `Medium Road` level 3, `Highway` (highways, ramps, toll roads, the map's highway connections) level 5. Large roads are presumably level 4. [inferred]
- **Layers** (`ItemClass.Layer`, bits): 0x1 default (roads, tracks, paths, power lines, decorative walls), 0x2 water pipes, 0x4 transport lines, 0x8 metro tunnels, 0x10 aircraft routes, 0x20 ship routes, and more for blimps, helicopters, fishing boats and ferries.
- **AI:** `NetAI.IsHighway()` returns `RoadBaseAI.m_highwayRules`; `IsBridge()` is true for the road and pedestrian bridge AIs; `IsTunnel()` only for `RoadTunnelAI`; `IsUnderground()` for the road, track, metro and pedestrian tunnel AIs and pedestrian paths built underground; `IsOverground()` for bridge AIs, monorails and cable cars. They are plain getters.
- **Elevation variants:** `RoadAI`, `TrainTrackAI`, `MetroTrackAI`, `PedestrianPathAI` and `PedestrianWayAI` have the fields `m_elevatedInfo`, `m_bridgeInfo`, `m_slopeInfo` and `m_tunnelInfo` (`RoadAI` also `m_connectedInfo`). The exporter reads them by name, so mods' AIs derived from these are covered.
- **Not only roads:** water pipes (`WaterPipeAI`), power lines (`PowerLineAI`), decorative walls, the routes of ships and aircraft (`ShipPathAI`, `FlightPathAI`, whose "connection" paths have a pedestrian lane too), and transport lines are networks as well. A transport line's prefab is `TransportInfo.m_netInfo`, such as `Bus Line` with `TransportLineAI` on layer 0x4; its nodes and segments are the line's stops and legs.
- **Categories:** the converter sorts prefabs for its debug render (`NetCategories`): only the default and metro-tunnel layers, never transport lines; roads by `IsHighway()` and level (levels 1 and 2 local, higher arterial); tracks by rail vehicle lanes; paths by pedestrian or bicycle lanes; the rest is not drawn.

## Mesh conventions

[community: [Road Editor wiki](https://skylines.paradoxwikis.com/Road_Editor), [cslmodding network page](https://cslmodding.info/asset/network/)]

- Segment meshes are straight stubs, usually 64 m long along Z and centred on the origin. X spans ±halfWidth and Y is up. They are usually subdivided about 16 times along Z, and `NetInfo.m_segmentLength` matches the mesh length.
- Pavement is 0.3 m high with its top at y = 0, so the road surface sits at about −0.3 m.
- Junction node meshes have a cut through the middle; the centre vertices move to the middle of the intersection.
- The V texture coordinate tiles about every 20 m. Vertex colour green = 0 disables that tiling.

## How the game renders them

The game draws network meshes in two ways, with the same bending maths [verified 2026-11-17, game 1.21.1-f9, IL read locally and checked numerically with the geometry probe; see Validation]:

- **Up close (GPU):** the segment and node meshes (`NetInfo.Segment.m_segmentMesh`, `NetInfo.Node.m_nodeMesh`; for vanilla roads the same objects as `m_mesh`) are drawn with `Graphics.DrawMesh` at a reference point, identity rotation, and per-instance shader inputs. The vertex shader bends the straight mesh.
- **Far away (CPU):** the render-group meshes. The static `NetSegment.PopulateGroupData(info, segmentInfo, left, right, meshScale, objectIndex, ...)` and the two static `NetNode.PopulateGroupData` overloads (two or four matrices) bend the prefab's combined level-of-detail data on the CPU. The instance methods `NetSegment/NetNode.PopulateGroupData(id, groupX, groupZ, layer, ...)` compute their inputs and call them, on the simulation thread (they use `NetNode`'s static `...BufferSimulation` scratch arrays; the renderer uses the `...BufferMain` ones).
- The level-of-detail data is `NetInfo.Segment/Node.m_combinedLod.m_key.m_mesh.m_data` (`RenderGroup.MeshData`), made from `m_lodMesh` by `NetInfo.GenerateCombinedLodMesh` with texture coordinates moved into an atlas. Vanilla road LOD meshes are tiny: 10 vertices for a segment, 12 for a node piece [verified].
- **Shader inputs** of a mesh draw [verified, `NetSegment.RenderSegments` and `NetNode.RenderInstance`]: `_LeftMatrix`, `_RightMatrix`, `_MeshScale`, `_ObjectIndex` and colours; junction and end pieces also `_LeftMatrixB`, `_RightMatrixB`, `_CenterPos`, `_SideScale`. They come from `RenderManager.Instance` fields:

| Shader input | Segment | Junction or end piece |
|---|---|---|
| `_LeftMatrix` | `m_dataMatrix0` | `m_dataMatrix0` |
| `_RightMatrix` | `m_dataMatrix1` | `m_extraData.m_dataMatrix2` |
| `_LeftMatrixB` | | `m_extraData.m_dataMatrix3` |
| `_RightMatrixB` | | `m_dataMatrix1` |
| `_MeshScale` | `m_dataVector0`, X and Y negated when the mesh is turned around | `m_dataVector0` |
| `_CenterPos` | | `m_dataVector1` |
| `_SideScale` | | `m_dataVector2` |
| draw position | `m_position`: the midpoint of the two node positions | `m_position`: the node position |

- `RenderManager.m_indices[holder]` is the first instance of a holder (65535: none) and `Instance.m_nextInstance` chains a node's pieces. Holders are 49152 + segment id and 86016 + node id; `RenderManager.GetColorLocation(holder)` gives the `_ObjectIndex` texture coordinates [verified].
- The vertex shader itself was not read. [inferred] It does what the CPU functions do: the inputs have the same roles and values (both paths compute them with the same game methods), and the far meshes are meant to look like the near ones. Checking a rendered frame against the formulas would settle it.

## Bending network meshes

[verified 2026-11-17: IL of the CPU functions read locally; reimplemented in `src/CsToBeamNG.Converter/Networks/NetMeshBending.cs` and compared with the game's own output, see Validation]

### Control matrices

`NetSegment.CalculateControlMatrix(a, b, c, d, aB, bB, cB, dB, reference, vScale)` returns a matrix whose columns 0–3 are the cubic Bézier control points a, b, c, d minus `reference`, in rows 0–2 (x, y, z). Row 3 holds a texture V coordinate per control point:

- The V offsets at the ends: V0 = dot(a − aB, b − a) / max(0.001, |b − a|) · vScale / 2, and likewise V3' at d with c and dB. They shift the texture so the two edges of a road line up.
- The V lengths of the three spans: the distances between the midpoints of (a, aB), (b, bB), (c, cB), (d, dB), times vScale / 2.
- The spans are scaled by f = (L − V0 − V3') · round(4L) / max(0.01, 4L²), where L is their sum, so a segment repeats its texture a whole number of quarter tiles.
- Row 3 is (V0, V0 + s1, V0 + s1 + s2, V0 + s1 + s2 + s3) with the scaled spans.
- `vScale` is `NetAI.GetVScale()` (0.05 for vanilla roads, about 20 m per tile [community]).
- The six-argument overload (single curve, no B points) scales its spans by a mix of the straight distance and the curve length; the game uses it for street names.

Multiplying a matrix with a vector of four weights, as Unity's `Matrix4x4 * Vector4`, gives the weighted sum of the control points (x, y, z) and of the V values (w).

### Segment formula

`NetSegment.PopulateGroupData` (static) and the two-matrix `NetNode.PopulateGroupData`, for each mesh vertex (x, y, z) with normal n and tangent tg:

1. s = x · meshScale.x + 0.5 (across, 0 at the left edge) and t = z · meshScale.y + 0.5 (along).
2. B(t) = ((1−t)³, 3t(1−t)², 3t²(1−t), t³), the cubic Bernstein weights, and B'(t) their derivatives.
3. P = lerp(Left · B, Right · B, s): a point (xyz) and a V (w). D = lerp(Left · B', Right · B', s), normalised: the direction along the road.
4. The frame: X = normalise(D.z, 0, −D.x) (horizontal, across), Y = D × X (up), Z = D.
5. The vertex goes to (P.x, P.y + y, P.z): mesh height is added unchanged, not along the frame's up. The normal and the tangent's xyz are mapped by the frame (X·n.x + Y·n.y + Z·n.z); the tangent's w stays.
6. The texture V becomes lerp(uv.y, P.w, green / 255), with the vertex colour's green; mesh data without colours counts as white. U stays.
7. If the prefab mesh has `m_requireHeightMap`, the height is replaced by the terrain's (`TerrainManager.SampleDetailHeight`).
8. Only in the two-matrix node function: a negative meshScale.x negates n.x and tg.x, a negative meshScale.y negates n.z and tg.z, before the frame.

The output is relative to the matrices' reference point. The game rewrites the vertex colours (`NetAI.GetGroupVertexColor`) and fills the second and fourth texture coordinates with the object index; those carry no geometry.

### Junction and end formula

The four-matrix `NetNode.PopulateGroupData`, for each node-mesh vertex (x, y, z). A node mesh is a segment-like stub whose −Z half (from the segment's end to the node) is used:

1. a = |x · meshScale.x| (0 at the middle line, 0.5 at the edges) and t = z · meshScale.y + 0.5.
2. Pavement matching: with the side's pair (p, w) = sideScale.xy for x < 0 and sideScale.zw otherwise, a' = (0.5 − p) · clamp01(a / meshScale.z) + p · clamp01((a − meshScale.z) / meshScale.w) + w · clamp01(a − 0.5), and a = lerp(a, a', t²). meshScale.z and .w are the prefab's road and pavement shares of the half width, so at t = 1 the road/pavement border moves to the share p, and widths beyond the edge scale by w.
3. s = a · sign(x) + 0.5 (sign(0) = 1), and t is halved: t' = t / 2 runs from 0 at the segment's end to 0.5 at the node.
4. side = smoothstep(−1, 1, x) with x in metres: 0 on the left half, 1 on the right half, blended across the middle 2 m. centre = smoothstep(0.3, 0.5, t' − |side − 0.5|).
5. The left curve is lerp(Left · B, LeftB · B, side), the right one lerp(Right · B, RightB · B, side); P = lerp(left, right, s), then P = lerp(P, centerPos, centre). The direction uses the derivatives in the same way, without the centre blend; if it is shorter than 0.1 it becomes +Z.
6. Frame, position (P + y), normals and tangents as for segments (no mirroring).
7. V = lerp(uv.y, min(0.9, P.w), green / 255).

So each half of a node mesh bends towards the neighbour on its own side (A matrices) and towards the other neighbour (B matrices); near the node's middle, everything converges on the centre position.

### Segment inputs

[verified: the CPU path in `NetSegment.PopulateGroupData`, the GPU path in `NetSegment.RenderInstance`; both call the same methods]

- Corners: `segment.CalculateCorner(id, heightOffset: true, start, leftSide, out pos, out dir, out smooth)` for the four corners. The left curve runs from the start's left corner to the end's *right* corner (the corner's side is seen from its own node), the right curve from the start's right corner to the end's left corner.
- Middle points: `NetSegment.CalculateMiddlePoints(startPos, startDir, endPos, endDir, smoothStart, smoothEnd, out m1, out m2)`, with the smoothness flags of the right-side corner calls.
- Left = `CalculateControlMatrix(startLeft, m1L, m2L, endRight, startRight, m1R, m2R, endLeft, reference, vScale)`; Right likewise with the roles swapped.
- meshScale = (0.5 / halfWidth, 1 / segmentLength, 1, 1).
- Which prefab meshes are drawn, and turned around, comes from `NetInfo.Segment.CheckFlags(segment.m_flags, segment.m_flags2, out turnAround)`. The CPU path builds the matrices from the end instead (end corners first, sides swapped) for a turned-around mesh; the GPU path negates meshScale.x and .y. Both mirror the mesh. The GPU path can also turn a mesh around for inverted segments (`NetAI.CanAutoRenderInverted`).
- Reference point: the render group's position (CPU) or the midpoint of the node positions (GPU).

### Junction inputs

[verified: CPU path in `NetNode.PopulateGroupData`; GPU path in `NetNode.RefreshJunctionData` (the three-argument glue and the per-piece six-argument overload)]

Every segment at a junction draws its own piece with the node meshes of *its* prefab (`segmentInfo.m_nodes`, filtered by `NetInfo.Node.CheckFlags(node.flags)` and `CheckTags`; direct-connect meshes separately):

- Neighbours: among the node's other segments with a compatible connection class (same service, and the same connect group if either prefab requires it), the one with the smallest turn to each side, by dot and cross products of the segments' directions at the node.
- Corners: the segment's two corners at the node, and each neighbour's two corners.
- Middle points between the segment's corners and the neighbours': the CPU path uses `CalculateMiddlePoints` as above. The GPU path uses the overload with ratios: 0.3 at both ends, scaled by the two prefabs' pavement shares (p₁, p₂ → 2p₂/(p₁+p₂) and 2p₁/(p₁+p₂)) and clamped to 0.05–0.7, unless the turn is sharp (neighbour score ≤ −0.5) or the prefab has no pavement. **So close-up junctions have different curves from their far meshes**: up to 1.2 m apart at the probed junctions, from this alone.
- Left = curve from the segment's left-side=false corner to the first neighbour's left-side=true corner; Right = from the left-side=true corner to that neighbour's other corner; LeftB and RightB the same towards the second neighbour. The B points of each matrix equal its own points.
- meshScale = (0.5 / halfWidth, 1 / segmentLength, 0.5 − p, p) with p = pavementWidth / halfWidth / 2.
- sideScale per side = ((p + p_neighbour) / 2, 2 · halfWidth / (halfWidth + halfWidth_neighbour)).
- Centre position: `NetNode.GetCenterPositions(nodeId, deltaDirs, out centers)` (private) gives, per segment, the node position plus the sum of the delta directions of the segment and its compatible neighbours, or, when there is exactly one compatible neighbour, the average of the midpoints of the two corner-to-corner curves; its height is then the node's height plus `m_heightOffset / 64`. A delta direction is the segment's direction times 2 + 2 · (largest dot product with a compatible neighbour), so it vanishes for a segment continued straight on.
  - The CPU path computes them every time.
  - The GPU path computes them (with a minimum length of `m_minCornerOffset · 0.4`) only for nodes with direct connections (`m_connectCount > 0`, tram and train tracks). For other junctions it reads its shared scratch buffer as the last such node left it. The probed junctions had none, and the renderer's centre was exactly the node position: the buffer held zeros. **So ordinary close-up junctions converge on the node position**, while their far meshes use the computed centre (up to 5.4 m apart at the probed junctions, at the middle vertices). [verified for this case; that the buffer is never written without direct connections is inferred from the code]
- Centre V: the CPU path uses the average of the four matrices' end V (m33); the GPU path the average of their middle V values (m31, m32), times 0.125 for eight values.
- Without any compatible neighbour, the piece is a cap like a dead end (below), with the centre between the segment's corners.

### End and bend inputs

[verified for the CPU path; the GPU methods `RefreshEndData` and `RefreshBendData` call the same methods in the same way, but also update fence instances in the renderer's buffer, so the probe doesn't call them]

- **Dead end:** the node's own prefab meshes (`nodeInfo.m_nodes`), with the four-matrix function. The four curves form a cap around the segment's two corners c0 (left-side=false) and c1: out of the node by r = `NetAI.GetEndRadius()` · 4/3 along the corner directions and back. Left = (c0, c0 − d0·r, c1 − d1·r, c1), Right = (c1, c1 + d1·r, c0 + d0·r, c0), LeftB = (c0, c0 + d0·r, c1 + d1·r, c1), RightB = (c1, c1 − d1·r, c0 − d0·r, c0); their V rows get +0.2; vScale is divided by 1.5. centerPos is the node position (plus the height offset), sideScale (p, 1, p, 1). The node flags get an extra bit per segment for its direction (`FlagsLong` 0x800000000 / 0x1000000000) before `CheckFlags`.
- **Bend:** the node's prefab segment meshes (`nodeInfo.m_segments`, unless `m_disableBendNodes`) drawn across the node from segment A (preferably the one that ends here) to segment B, with `CheckFlags(netAI.GetBendFlags(...), netAI.GetEventFlags(...))`; a turned-around mesh gets a negative meshScale (the CPU segment function then leaves its normals unmirrored). Direct-connect node meshes of the node's prefab are drawn with the two-matrix node function, one-way ones only in their direction.
- **Middle nodes** draw nothing of their own.

### Validation

The geometry probe (`cstobeamng export <save> --probe geometry`, then `cstobeamng dev geometry-probe <dump>`) picks road objects automatically and compares the converter's bending with the game's CPU functions. CityPalma, 2026-11-17, game 1.21.1-f9 [verified]:

- Objects: a straight 102 m highway segment, a 90° curve, a highway segment rising 18.6 m over 84.7 m, a segment drawn turned around, a 4-way junction of medium and basic roads, a T-junction, a highway/ramp junction of different widths, a T-junction the renderer had drawn, a 135° bend and a dead end.
- Pieces: the game's output for each object equals the probe's pieces together, vertex for vertex. So the probe computes the inputs as the game does.
- Bending of the level-of-detail meshes (10–12 vertices) and of the main meshes (130–435 vertices, through a stand-in prefab mesh): largest position error 8 µm over 61 comparisons, normals within 1e-5, texture coordinates within 1e-6.
- Junction main meshes with the renderer's inputs, computed by calling the game's private per-piece method: the same accuracy. For the two junctions the game had drawn, the renderer's own instance data (read from `RenderManager.m_instances`) equals these inputs in every matrix and vector element, once the probe used the renderer's delta-direction rule (above); before that, the centre position was off by 5.4 m at one of them.
- Close-up against far shape of the same junction pieces: up to 5.4 m apart (centre), 1.2 m where only the middle points differ.

### Readability

Vanilla network meshes are CPU-readable (`Mesh.isReadable` true): segment, node and LOD meshes of the basic, medium and decorated roads, highways and ramps [verified 2026-11-17].

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

- The vertex shader: assumed to match the CPU functions; compare a rendered frame with the formulas to confirm.
- Direct-connect meshes (tram and train tracks across junctions, `m_requireDirectRenderers`) and their junction pieces: not probed.
- Bridges, tunnels, slopes, rails, paths and other AIs: not probed; the functions are the same, but their prefabs and flags differ.
- Segments whose meshes follow the terrain (`m_requireHeightMap`): none were probed.
- Workshop network meshes may not be readable; vanilla ones are.
- `m_elevation`: what exactly it is relative to (the ground when the node was built, or the raw terrain), and how the game uses it.
- The large roads' class level (presumably 4); none in the dumps so far.
- How `TransportLineAI` computes its lanes' lengths.
