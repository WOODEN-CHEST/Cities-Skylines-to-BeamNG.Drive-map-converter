using System;
using System.Numerics;

namespace CsToBeamNG.Converter.BeamNG.Scene;

/// <summary>
/// A spawn point. <c>info.json</c> refers to it by <see cref="SceneObject.Name"/>, and the player's vehicle spawns at
/// its position with its rotation. Vehicles face their local −Y axis, so use <see cref="RotationForHeading"/>.
/// </summary>
public sealed class SpawnSphere : SceneObject
{
    private readonly float _radius = 1;

    /// <inheritdoc/>
    public override string ClassName => "SpawnSphere";

    /// <summary>Where the vehicle spawns.</summary>
    public required Vector3 Position { get; init; }

    /// <summary>The rotation the vehicle spawns with; see <see cref="RotationForHeading"/>.</summary>
    public RotationMatrix Rotation { get; init; } = RotationMatrix.Identity;

    /// <summary>The radius of the marker the editor shows.</summary>
    public float Radius
    {
        get => _radius;
        init => _radius = value > 0 && float.IsFinite(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Radius), value, "The radius must be positive.");
    }

    internal override Vector3? ScenePosition => Position;

    /// <summary>The rotation that makes a vehicle spawned here face <paramref name="heading"/>.</summary>
    /// <param name="heading">The horizontal direction the vehicle should face.</param>
    /// <returns>A rotation whose local −Y axis points along <paramref name="heading"/>.</returns>
    /// <remarks>
    /// A vehicle spawns with the sphere's rotation and faces its local −Y axis (verified in BeamNG.drive 0.40.5;
    /// docs/research/beamng-level-format.md, Scene objects).
    /// </remarks>
    public static RotationMatrix RotationForHeading(Vector2 heading) => RotationMatrix.YawTowards(-heading);

    internal override void AddFields(SceneFields fields)
    {
        // The datablock that every official spawn point uses.
        fields.Set("dataBlock", "SpawnSphereMarker");
        fields.Set("radius", Radius);
        fields.Set("rotationMatrix", Rotation.ToJson());
    }
}
