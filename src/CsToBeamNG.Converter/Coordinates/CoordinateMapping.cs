using System;
using System.Numerics;
using CsToBeamNG.Converter.BeamNG.Scene;

namespace CsToBeamNG.Converter.Coordinates;

/// <summary>
/// Converts Cities: Skylines world coordinates into BeamNG.drive's, and positions back. This class is the only
/// place that knows how the two games' axes relate (docs/architecture.md, Coordinate systems).
/// </summary>
/// <remarks>
/// <para>
/// Cities: Skylines uses Unity's world space: metres, Y up, left-handed, X and Z horizontal, with the origin at
/// the map's centre (docs/research/cs1-save-and-world.md, World geometry). BeamNG.drive uses metres with Z up and
/// is right-handed. The mapping is BeamNG (x, y, z) = CS1 (x, z, y): CS1's +X stays +X and its +Z becomes
/// BeamNG's +Y, so the map seen from above isn't mirrored. The origin and the heights stay as they are.
/// </para>
/// <para>
/// The swap mirrors the coordinates (its determinant is −1) while the handedness changes too, so shapes keep
/// their real-world form; but the order in which a triangle's vertices appear from its front side reverses
/// relative to each engine's rule, and so does the sign of a tangent's bitangent.
/// </para>
/// <para>
/// <b>To verify:</b> the mapping, the winding and the rotation follow from the two games' conventions, but no
/// generated level has confirmed them yet. Step 1.9 checks them with an asymmetric object. Until then, convert
/// only through this class, so that a correction stays in one place.
/// </para>
/// </remarks>
public static class CoordinateMapping
{
    /// <summary>Converts a position from CS1 world coordinates to BeamNG world coordinates.</summary>
    /// <param name="position">A position in CS1 world coordinates, in metres.</param>
    public static Vector3 ToBeamNG(Vector3 position) => SwapAxes(position);

    /// <summary>Converts a position from BeamNG world coordinates back to CS1 world coordinates.</summary>
    /// <param name="position">A position in BeamNG world coordinates, in metres.</param>
    public static Vector3 ToCitiesSkylines(Vector3 position) => SwapAxes(position);

    /// <summary>
    /// Converts a direction, such as a normal, a tangent's direction or an offset between two points. Unlike a
    /// position, a direction would ignore a shift of the origin if the mapping ever got one.
    /// </summary>
    /// <param name="direction">A direction in CS1 world or mesh coordinates.</param>
    public static Vector3 DirectionToBeamNG(Vector3 direction) => SwapAxes(direction);

    /// <summary>
    /// Converts a Unity tangent: the direction like any other, and the handedness in <c>W</c> negated, because
    /// the bitangent, computed as cross(normal, tangent) × W, keeps its direction only if W changes sign when the
    /// coordinates are mirrored.
    /// </summary>
    /// <param name="tangent">A tangent with the direction in X, Y and Z and the handedness (±1) in W.</param>
    public static Vector4 TangentToBeamNG(Vector4 tangent)
    {
        Vector3 direction = SwapAxes(new Vector3(tangent.X, tangent.Y, tangent.Z));
        return new Vector4(direction, -tangent.W);
    }

    /// <summary>
    /// Reverses the winding of triangles in place: Unity treats triangles whose vertices appear clockwise as
    /// front-facing, Collada and BeamNG counter-clockwise ones. The first vertex of each triangle stays first.
    /// </summary>
    /// <param name="triangles">The triangles' vertex indices, three per triangle.</param>
    /// <exception cref="ArgumentException">The number of indices isn't a multiple of three.</exception>
    public static void ReverseWinding(Span<int> triangles)
    {
        if (triangles.Length % 3 != 0)
        {
            throw new ArgumentException("Triangles have three vertex indices each.", nameof(triangles));
        }

        for (int i = 0; i < triangles.Length; i += 3)
        {
            (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
        }
    }

    /// <summary>
    /// Converts the angle by which CS1 turns an object about the vertical axis into BeamNG's yaw: the angle of a
    /// rotation about BeamNG's +Z axis, counter-clockwise seen from above.
    /// </summary>
    /// <remarks>
    /// CS1 rotates buildings and props with <c>AngleAxis(angle, Vector3.down)</c> (docs/research/cs1-assets.md,
    /// Colour variation and randomness), which turns them counter-clockwise seen from above; with the axis swap,
    /// that is the same angle about BeamNG's +Z. To verify (step 1.9).
    /// </remarks>
    /// <param name="angleRadians">The object's angle in CS1, in radians, as the game stores it.</param>
    /// <returns>The yaw in radians.</returns>
    public static float YawToBeamNG(float angleRadians) => angleRadians;

    /// <summary>
    /// Returns BeamNG's rotation for an object that CS1 turns by an angle about the vertical axis: a rotation
    /// about +Z by <see cref="YawToBeamNG"/>. The object's mesh must be converted with this class as well.
    /// </summary>
    /// <param name="angleRadians">The object's angle in CS1, in radians, at most 10⁶ in magnitude.</param>
    /// <returns>The rotation. It is computed without the platform's trigonometry, so it is the same everywhere.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The angle isn't finite or is too large.</exception>
    public static RotationMatrix RotationToBeamNG(float angleRadians)
    {
        (double sin, double cos) = DeterministicTrig.SinCos(YawToBeamNG(angleRadians));
        float s = (float)sin;
        float c = (float)cos;

        // Local X turns to (c, s, 0) and local Y to (−s, c, 0); the columns are the local axes.
        return new RotationMatrix(
            c, -s, 0,
            s, c, 0,
            0, 0, 1);
    }

    // The one definition of the axis mapping: BeamNG (x, y, z) = CS1 (x, z, y). It is its own inverse.
    private static Vector3 SwapAxes(Vector3 value) => new(value.X, value.Z, value.Y);
}
