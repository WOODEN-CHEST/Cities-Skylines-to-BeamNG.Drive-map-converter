using CsToBeamNG.Converter.Coordinates;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.World;

/// <summary>
/// The horizontal frame of an object that the game turns by an angle about the downward axis, as it does buildings and
/// props (<c>Quaternion.AngleAxis(angle, Vector3.down)</c>; docs/research/cs1-assets.md, Placement), in the game's X
/// and Z. The object's local +X lies towards (cos angle, sin angle) and its local +Z, the front of a building, towards
/// (−sin angle, cos angle).
/// </summary>
public readonly struct ObjectFrame
{
    private readonly double _x;
    private readonly double _z;
    private readonly double _sin;
    private readonly double _cos;

    /// <summary>Creates the frame of an object.</summary>
    /// <param name="origin">The object's position; only X and Z are used.</param>
    /// <param name="angleRadians">The object's angle as the game stores it, at most 10⁶ in magnitude.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">The angle isn't finite or is too large.</exception>
    public ObjectFrame(DumpVector3 origin, float angleRadians)
    {
        _x = origin.X;
        _z = origin.Z;
        (_sin, _cos) = DeterministicTrig.SinCos(angleRadians);
    }

    /// <summary>The direction of the object's local +X axis.</summary>
    public (double X, double Z) Right => (_cos, _sin);

    /// <summary>The direction of the object's local +Z axis: where a building's front faces.</summary>
    public (double X, double Z) Forward => (-_sin, _cos);

    /// <summary>Returns where a point given in the object's own coordinates lies on the map.</summary>
    /// <param name="localX">The point's local X, in metres.</param>
    /// <param name="localZ">The point's local Z, in metres.</param>
    public (double X, double Z) ToWorld(double localX, double localZ) =>
        (_x + (localX * _cos) - (localZ * _sin), _z + (localX * _sin) + (localZ * _cos));
}
