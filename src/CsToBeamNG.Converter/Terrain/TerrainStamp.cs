using CsToBeamNG.Converter.World;

namespace CsToBeamNG.Converter.Terrain;

/// <summary>
/// One edit of a <see cref="TerrainEditLayer"/>: a rule that gives the terrain a new height at the positions it
/// covers. Positions and heights are in the city's coordinates (metres).
/// </summary>
/// <remarks>Implementations must be deterministic and use only arithmetic (agents/conventions.md, determinism).</remarks>
public abstract class TerrainStamp
{
    /// <summary>The area the stamp may change, falloff included, in the city's X and Z. Samples outside keep their height.</summary>
    public abstract MapRect Bounds { get; }

    /// <summary>Returns the terrain's new height at a position.</summary>
    /// <param name="x">The city X, in metres.</param>
    /// <param name="z">The city Z, in metres.</param>
    /// <param name="height">The height there so far, in metres.</param>
    /// <returns>The new height, in metres; <paramref name="height"/> to leave it.</returns>
    public abstract double Apply(double x, double z, double height);
}
