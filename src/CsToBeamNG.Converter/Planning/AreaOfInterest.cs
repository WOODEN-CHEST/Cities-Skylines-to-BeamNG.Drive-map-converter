using System;
using CsToBeamNG.Converter.World;

namespace CsToBeamNG.Converter.Planning;

/// <summary>
/// The part of the map that the conversion covers: a rectangle in CS1's horizontal coordinates
/// (docs/architecture.md, Scale strategy). CS1 maps are 17.28 km across, far larger than a BeamNG level should be,
/// so by default only the built-up area and a margin around it are converted.
/// </summary>
/// <param name="Bounds">The area, within the map.</param>
/// <param name="Source">Where the area came from.</param>
/// <param name="ContentBounds">The union of the sections' content bounds before the margin, if any section gave bounds.</param>
public sealed record AreaOfInterest(MapRect Bounds, AreaOfInterestSource Source, MapRect? ContentBounds)
{
    /// <summary>Plans the area of interest of a city.</summary>
    /// <remarks>
    /// The first of these that applies:
    /// <list type="number">
    /// <item><description>the tiles in <see cref="AreaOfInterestOptions.Tiles"/>;</description></item>
    /// <item><description>
    /// the union of the sections' <see cref="ISectionData.AreaOfInterestBounds"/>, grown by the margin and clamped to
    /// the map;
    /// </description></item>
    /// <item><description>the city's unlocked tiles, which bound where it can have built;</description></item>
    /// <item><description>the whole map.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="world">The world model.</param>
    /// <param name="options">Options, or <see langword="null"/> for <see cref="AreaOfInterestOptions.Default"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The margin is negative or not finite.</exception>
    public static AreaOfInterest Plan(CityWorld world, AreaOfInterestOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        options ??= AreaOfInterestOptions.Default;
        if (!(options.MarginMeters >= 0) || !float.IsFinite(options.MarginMeters))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MarginMeters, "The margin must be finite and not negative.");
        }

        MapRect? content = null;
        foreach (ISectionData section in world.Sections)
        {
            if (section.AreaOfInterestBounds is { } bounds)
            {
                content = content?.Union(bounds) ?? bounds;
            }
        }

        if (options.Tiles is { Count: > 0 } && MapGeometry.TileBounds(options.Tiles) is { } chosen)
        {
            return new AreaOfInterest(chosen, AreaOfInterestSource.Tiles, content);
        }

        if (content?.Expand(options.MarginMeters).Intersect(MapGeometry.Bounds) is { } clamped)
        {
            return new AreaOfInterest(clamped, AreaOfInterestSource.Content, content);
        }

        if (MapGeometry.TileBounds(world.UnlockedTiles) is { } unlocked)
        {
            return new AreaOfInterest(unlocked, AreaOfInterestSource.UnlockedTiles, content);
        }

        return new AreaOfInterest(MapGeometry.Bounds, AreaOfInterestSource.Map, content);
    }
}
