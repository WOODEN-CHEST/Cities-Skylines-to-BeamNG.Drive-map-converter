using System;
using CsToBeamNG.Converter.World;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Buildings;

/// <summary>
/// The lot a building stands on: <see cref="BuildingRecord.Width"/> × <see cref="BuildingRecord.Length"/> cells of 8 m,
/// centred on the building's position and turned by its angle, with its front, which faces the road, on the building's
/// local +Z side (docs/research/cs1-assets.md, Placement).
/// </summary>
public static class BuildingLot
{
    /// <summary>The number of corners <see cref="Corners"/> writes.</summary>
    public const int CornerCount = 4;

    /// <summary>The lot's width across its front, in metres.</summary>
    /// <param name="building">The building.</param>
    public static double WidthMeters(BuildingRecord building)
    {
        ArgumentNullException.ThrowIfNull(building);
        return building.Width * (double)BuildingRecord.CellSizeMeters;
    }

    /// <summary>The lot's length from its front to its back, in metres.</summary>
    /// <param name="building">The building.</param>
    public static double LengthMeters(BuildingRecord building)
    {
        ArgumentNullException.ThrowIfNull(building);
        return building.Length * (double)BuildingRecord.CellSizeMeters;
    }

    /// <summary>
    /// Writes the lot's corners in the game's X and Z, going round the lot: the front edge's two ends first, then the
    /// back edge's, so that corners 0 and 1 are the front edge.
    /// </summary>
    /// <param name="building">The building; its angle must be finite.</param>
    /// <param name="corners">Where to write the <see cref="CornerCount"/> corners.</param>
    /// <exception cref="ArgumentException"><paramref name="corners"/> is too short.</exception>
    public static void Corners(BuildingRecord building, Span<(double X, double Z)> corners)
    {
        ArgumentNullException.ThrowIfNull(building);
        if (corners.Length < CornerCount)
        {
            throw new ArgumentException("There must be room for four corners.", nameof(corners));
        }

        var frame = new ObjectFrame(building.Position, building.Angle);
        double halfWidth = WidthMeters(building) / 2;
        double halfLength = LengthMeters(building) / 2;
        corners[0] = frame.ToWorld(-halfWidth, halfLength);
        corners[1] = frame.ToWorld(halfWidth, halfLength);
        corners[2] = frame.ToWorld(halfWidth, -halfLength);
        corners[3] = frame.ToWorld(-halfWidth, -halfLength);
    }

    /// <summary>
    /// Returns the middle of the lot's front edge, where the game's <c>Building.CalculateSidewalkPosition</c> puts the
    /// sidewalk.
    /// </summary>
    /// <param name="building">The building; its angle must be finite.</param>
    public static (double X, double Z) FrontMiddle(BuildingRecord building)
    {
        ArgumentNullException.ThrowIfNull(building);
        return new ObjectFrame(building.Position, building.Angle).ToWorld(0, LengthMeters(building) / 2);
    }
}
