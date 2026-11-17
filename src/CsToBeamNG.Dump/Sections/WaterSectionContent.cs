using System.Collections.Generic;

namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The content of a <c>water</c> section (docs/dump-format.md, <c>water</c>): what <see cref="WaterSectionWriter"/>
/// writes and the .NET 10 build's <c>WaterSectionReader</c> returns. The cell arrays have one value per point of
/// <see cref="Grid"/>, row by row; heights are in 1/64 m (<see cref="WaterSection.MetersPerHeightUnit"/>).
/// </summary>
public sealed class WaterSectionContent
{
    /// <summary>Creates the content.</summary>
    /// <param name="seaLevel">The sea level, in metres.</param>
    /// <param name="grid">Where the cells lie.</param>
    /// <param name="ground">Each cell's ground: the bottom the water simulation fills, in 1/64 m.</param>
    /// <param name="depth">Each cell's water depth above <paramref name="ground"/>, in 1/64 m; 0 where it is dry.</param>
    /// <param name="pollution">Each cell's pollution, in the game's units.</param>
    /// <param name="velocityX">Each cell's flow along X, in the game's units.</param>
    /// <param name="velocityZ">Each cell's flow along Z, in the game's units.</param>
    public WaterSectionContent(
        float seaLevel,
        GridGeometry grid,
        ushort[] ground,
        ushort[] depth,
        ushort[] pollution,
        short[] velocityX,
        short[] velocityZ)
    {
        Guard.NotNull(grid, nameof(grid));
        Guard.NotNull(ground, nameof(ground));
        Guard.NotNull(depth, nameof(depth));
        Guard.NotNull(pollution, nameof(pollution));
        Guard.NotNull(velocityX, nameof(velocityX));
        Guard.NotNull(velocityZ, nameof(velocityZ));
        SeaLevel = seaLevel;
        Grid = grid;
        Ground = ground;
        Depth = depth;
        Pollution = pollution;
        VelocityX = velocityX;
        VelocityZ = velocityZ;
    }

    /// <summary>The sea level, in metres: the water level the simulation keeps at the map's edges.</summary>
    public float SeaLevel { get; }

    /// <summary>Where the cells lie.</summary>
    public GridGeometry Grid { get; }

    /// <summary>Each cell's ground in 1/64 m: the bottom the water simulation fills.</summary>
    public ushort[] Ground { get; }

    /// <summary>Each cell's water depth above its ground in 1/64 m; 0 where it is dry.</summary>
    public ushort[] Depth { get; }

    /// <summary>Each cell's pollution, in the game's units.</summary>
    public ushort[] Pollution { get; }

    /// <summary>Each cell's flow along X, in the game's units.</summary>
    public short[] VelocityX { get; }

    /// <summary>Each cell's flow along Z, in the game's units.</summary>
    public short[] VelocityZ { get; }

    /// <summary>The water sources, ordered by id.</summary>
    public IList<WaterSourceRecord> Sources { get; } = new List<WaterSourceRecord>();
}
