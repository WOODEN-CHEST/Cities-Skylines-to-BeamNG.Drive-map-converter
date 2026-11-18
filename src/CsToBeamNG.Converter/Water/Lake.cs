using System.Collections.Generic;
using CsToBeamNG.Converter.World;

namespace CsToBeamNG.Converter.Water;

/// <summary>A flat body of water other than the sea, as <see cref="WaterBodyFinder"/> found it.</summary>
/// <param name="Surface">Where the game draws its water, in metres: the cells' median surface less the drawn offset.</param>
/// <param name="Bottom">A height below all the ground around it, in metres, down to which its box of water reaches.</param>
/// <param name="CellCount">The number of the water simulation's cells it has within the area looked at.</param>
/// <param name="AreaSquareMeters">The area of those cells.</param>
/// <param name="Rectangles">
/// Rectangles that together cover its cells and the shore cells around them, in city X and Z, clipped to the area
/// looked at, ordered by Z, then X. They don't cover dry ground lower than its surface.
/// </param>
public sealed record Lake(double Surface, double Bottom, int CellCount, double AreaSquareMeters, IReadOnlyList<MapRect> Rectangles)
{
    /// <summary>The smallest rectangle that contains all of <see cref="Rectangles"/>.</summary>
    public MapRect Bounds
    {
        get
        {
            MapRect bounds = Rectangles[0];
            foreach (MapRect rectangle in Rectangles)
            {
                bounds = bounds.Union(rectangle);
            }

            return bounds;
        }
    }
}
