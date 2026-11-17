using System;
using System.Numerics;

namespace CsToBeamNG.Converter.BeamNG.Scene;

/// <summary>A static mesh placed in the world (docs/research/beamng-level-format.md, Meshes).</summary>
public sealed class TSStatic : SceneObject
{
    /// <inheritdoc/>
    public override string ClassName => "TSStatic";

    /// <summary>The mesh file, a <c>.dae</c>.</summary>
    public required VirtualPath ShapeName { get; init; }

    /// <summary>The position of the mesh's origin.</summary>
    public required Vector3 Position { get; init; }

    /// <summary>The orientation.</summary>
    public RotationMatrix Rotation { get; init; } = RotationMatrix.Identity;

    /// <summary>The scale along the local axes.</summary>
    public Vector3 Scale { get; init; } = Vector3.One;

    /// <summary>What vehicles collide with.</summary>
    public TSStaticCollisionType CollisionType { get; init; } = TSStaticCollisionType.CollisionMesh;

    internal override Vector3? ScenePosition => Position;

    internal override void AddFields(SceneFields fields)
    {
        fields.Set("collisionType", ToFieldValue(CollisionType));
        fields.Set("rotationMatrix", Rotation.ToJson());
        fields.Set("scale", Scale);
        fields.Set("shapeName", ShapeName.Value);
    }

    private static string ToFieldValue(TSStaticCollisionType type) => type switch
    {
        TSStaticCollisionType.None => "None",
        TSStaticCollisionType.Bounds => "Bounds",
        TSStaticCollisionType.CollisionMesh => "Collision Mesh",
        TSStaticCollisionType.VisibleMesh => "Visible Mesh",
        TSStaticCollisionType.VisibleMeshFinal => "Visible Mesh Final",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown collision type."),
    };
}
