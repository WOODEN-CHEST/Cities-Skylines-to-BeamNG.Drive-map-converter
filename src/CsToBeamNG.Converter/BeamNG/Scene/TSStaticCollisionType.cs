namespace CsToBeamNG.Converter.BeamNG.Scene;

/// <summary>
/// What a <see cref="TSStatic"/>'s collision uses (its <c>collisionType</c> field). The values are the game's
/// options (official TSStatic documentation); docs/research/beamng-level-format.md notes what is verified.
/// </summary>
public enum TSStaticCollisionType
{
    /// <summary>No collision.</summary>
    None,

    /// <summary>The bounding box.</summary>
    Bounds,

    /// <summary>The shape's collision meshes (<c>Colmesh-1</c> and similar): the game's default, and its recommendation.</summary>
    CollisionMesh,

    /// <summary>The visible mesh (<c>Visible Mesh</c>).</summary>
    VisibleMesh,

    /// <summary>The visible mesh (<c>Visible Mesh Final</c>).</summary>
    VisibleMeshFinal,
}
