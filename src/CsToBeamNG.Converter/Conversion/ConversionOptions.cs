using System;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.Water;
using CsToBeamNG.Converter.World;

namespace CsToBeamNG.Converter.Conversion;

/// <summary>What <see cref="CityLevelConverter.Convert"/> makes of a city.</summary>
public sealed record ConversionOptions
{
    private readonly string _levelName = LevelNames.FromCityName(null);

    /// <summary>
    /// The level's name, which is also its mod folder's name, such as <c>cstobeamng_san_palma</c>
    /// (<see cref="LevelNames"/>).
    /// </summary>
    public required string LevelName
    {
        get => _levelName;
        init => _levelName = LevelNames.IsLevelName(value)
            ? value
            : throw new ArgumentException($"'{value}' isn't a level name; see LevelNames.", nameof(LevelName));
    }

    /// <summary>The part of the map to convert, in city X and Z, or <see langword="null"/> for the area of interest.</summary>
    public MapRect? Area { get; init; }

    /// <summary>How the report names <see cref="Area"/>, such as "tiles 3-5,4"; ignored without an area.</summary>
    public string? AreaDescription { get; init; }

    /// <summary>The choices for the terrain grid.</summary>
    public TerrainGridOptions TerrainGrid { get; init; } = TerrainGridOptions.Default;

    /// <summary>How the 16 m heights are read where the game has no 4 m detail heights.</summary>
    public CoarseInterpolation CoarseInterpolation { get; init; } = CoarseInterpolation.MonotoneCubic;

    /// <summary>The rules for the terrain's materials.</summary>
    public TerrainSurfaceRules SurfaceRules { get; init; } = TerrainSurfaceRules.Default;

    /// <summary>The thresholds for sorting the city's water.</summary>
    public WaterBodyOptions Water { get; init; } = WaterBodyOptions.Default;

    /// <summary>Edits to apply to the terrain's heights, such as road beds; none so far.</summary>
    public TerrainEditLayer? TerrainEdits { get; init; }
}
