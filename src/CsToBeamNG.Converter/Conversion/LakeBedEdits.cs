using System;
using System.Numerics;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.Water;
using CsToBeamNG.Converter.World;

namespace CsToBeamNG.Converter.Conversion;

/// <summary>
/// Terrain edits that keep the sea plane out of lakes. The sea is an endless plane, so wherever a lake's bed lies below
/// the sea's surface, the sea's water lies under the lake's; BeamNG.drive then fails to draw the lake's water over that
/// part of its bed (verified in 0.40.5; docs/research/beamng-level-format.md, Water). Raising those parts of the bed to
/// just above the sea keeps the two apart; they lie deep under the lake's water, where the change can't be seen.
/// </summary>
public static class LakeBedEdits
{
    /// <summary>How far above the sea's surface a lake's bed is raised to, in metres.</summary>
    public const double ClearanceMeters = 0.5;

    /// <summary>
    /// Returns the edits for a city's water: under each lake's boxes of water, ground lower than the sea's surface plus
    /// <see cref="ClearanceMeters"/> is raised to that height. Without a sea there is nothing to do.
    /// </summary>
    /// <param name="water">The sorted water.</param>
    /// <returns>The edits, one stamp per lake whose bed may reach below the sea, in the lakes' order.</returns>
    public static TerrainEditLayer Create(WaterBodies water)
    {
        ArgumentNullException.ThrowIfNull(water);
        var layer = new TerrainEditLayer();
        if (water.SeaSurface is not double sea)
        {
            return layer;
        }

        float height = (float)(sea + ClearanceMeters);
        foreach (Lake lake in water.Lakes)
        {
            if (lake.Surface <= height || lake.Bottom >= height)
            {
                continue;
            }

            var corners = new Vector3[lake.Rectangles.Count * 6];
            for (int n = 0; n < lake.Rectangles.Count; n++)
            {
                MapRect r = lake.Rectangles[n];
                var a = new Vector3(r.MinX, height, r.MinZ);
                var b = new Vector3(r.MaxX, height, r.MinZ);
                var c = new Vector3(r.MaxX, height, r.MaxZ);
                var d = new Vector3(r.MinX, height, r.MaxZ);
                ReadOnlySpan<Vector3> triangles = [a, b, c, a, c, d];
                triangles.CopyTo(corners.AsSpan(n * 6));
            }

            layer.Add(new FootprintStamp(corners, StampMode.Raise, falloffMeters: 0));
        }

        return layer;
    }
}
