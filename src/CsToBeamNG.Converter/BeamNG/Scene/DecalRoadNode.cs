using System.Numerics;

namespace CsToBeamNG.Converter.BeamNG.Scene;

/// <summary>A node of a <see cref="DecalRoad"/>: <c>[x, y, z, width]</c> in its <c>nodes</c> field.</summary>
/// <param name="Position">The centre of the road at this node.</param>
/// <param name="Width">The full width of the road at this node, in metres.</param>
public readonly record struct DecalRoadNode(Vector3 Position, float Width);
