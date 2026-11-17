using System;
using System.Numerics;
using System.Text.Json.Nodes;

namespace CsToBeamNG.Converter.BeamNG.Scene;

/// <summary>
/// The orientation of a scene object: a 3 × 3 rotation matrix that maps the object's local axes to world axes,
/// applied to column vectors (world = M × local). <c>M12</c> is row 1, column 2; column 1 is where local +X points.
/// </summary>
/// <remarks>
/// The <c>rotationMatrix</c> field lists the columns one after another, that is the world directions of the local
/// X, Y and Z axes: <c>[M11, M21, M31, M12, M22, M32, M13, M23, M33]</c>. Verified in BeamNG.drive 0.40.5 by reading
/// a loaded object's transform (docs/research/beamng-level-format.md, Scene objects). BeamNG's axes are
/// right-handed with Z up.
/// </remarks>
/// <param name="M11">Row 1, column 1.</param>
/// <param name="M12">Row 1, column 2.</param>
/// <param name="M13">Row 1, column 3.</param>
/// <param name="M21">Row 2, column 1.</param>
/// <param name="M22">Row 2, column 2.</param>
/// <param name="M23">Row 2, column 3.</param>
/// <param name="M31">Row 3, column 1.</param>
/// <param name="M32">Row 3, column 2.</param>
/// <param name="M33">Row 3, column 3.</param>
public readonly record struct RotationMatrix(
    float M11, float M12, float M13,
    float M21, float M22, float M23,
    float M31, float M32, float M33)
{
    /// <summary>No rotation.</summary>
    public static RotationMatrix Identity { get; } = new(1, 0, 0, 0, 1, 0, 0, 0, 1);

    /// <summary>The world direction of the local +X axis (column 1).</summary>
    public Vector3 LocalXAxis => new(M11, M21, M31);

    /// <summary>The world direction of the local +Y axis (column 2).</summary>
    public Vector3 LocalYAxis => new(M12, M22, M32);

    /// <summary>The world direction of the local +Z axis (column 3).</summary>
    public Vector3 LocalZAxis => new(M13, M23, M33);

    /// <summary>A rotation about the vertical axis that turns the local +Y axis towards <paramref name="direction"/>.</summary>
    /// <param name="direction">A horizontal direction (x, y); it needn't be normalized but must not be zero.</param>
    /// <returns>The rotation. It uses no trigonometry, so it is identical on every platform.</returns>
    /// <exception cref="ArgumentException">The direction is zero or not finite.</exception>
    public static RotationMatrix YawTowards(Vector2 direction)
    {
        float length = direction.Length();
        if (!(length > 0) || !float.IsFinite(length))
        {
            throw new ArgumentException("The direction must be finite and not zero.", nameof(direction));
        }

        // Columns: local X goes to (y, -x, 0), local Y to (x, y, 0), local Z stays up.
        float x = direction.X / length;
        float y = direction.Y / length;
        return new RotationMatrix(
            y, x, 0,
            -x, y, 0,
            0, 0, 1);
    }

    /// <summary>The <c>rotationMatrix</c> field value: the columns one after another.</summary>
    internal JsonArray ToJson() => BeamNGJson.Floats(M11, M21, M31, M12, M22, M32, M13, M23, M33);
}
