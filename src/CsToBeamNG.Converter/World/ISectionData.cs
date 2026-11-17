using System.Collections.Generic;

namespace CsToBeamNG.Converter.World;

/// <summary>
/// The data of one dump section in the world model, as its loader produced it (<see cref="Loading.ISectionLoader"/>).
/// Values stay game-native: CS1 coordinates, units and angles (docs/dump-format.md, Data).
/// </summary>
/// <remarks>
/// Each section type implements this with its own data, such as heights or road segments. The world model holds
/// whatever was loaded; stages find it by type (<see cref="CityWorld.Find{T}"/>).
/// </remarks>
public interface ISectionData
{
    /// <summary>Figures for <c>cstobeamng inspect</c>, such as counts and value ranges, in a fixed order.</summary>
    IReadOnlyList<SectionStatistic> Statistics { get; }

    /// <summary>
    /// The horizontal extent of the content that the area of interest must include, such as the bounds of the
    /// buildings, or <see langword="null"/> if the section doesn't define where the city is. Sections that cover
    /// the whole map, such as the terrain, return <see langword="null"/> (docs/architecture.md, Scale strategy).
    /// </summary>
    MapRect? AreaOfInterestBounds { get; }
}
