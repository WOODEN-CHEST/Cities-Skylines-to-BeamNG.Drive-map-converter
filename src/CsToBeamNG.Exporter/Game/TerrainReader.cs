using System;
using System.Collections.Generic;
using System.Globalization;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Exporter.Game;

/// <summary>
/// Reads the terrain and the water simulation into the contents of the dump's <c>terrain</c> and <c>water</c>
/// sections, without changing anything (docs/research/cs1-save-and-world.md, Terrain and Water). Create it on the main
/// thread; read on the simulation thread, where the game changes the terrain.
/// </summary>
internal sealed class TerrainReader
{
    private readonly TerrainManager _terrain;

    private TerrainReader(TerrainManager terrain) => _terrain = terrain;

    /// <summary>Finds the terrain manager. Main thread only (<see cref="CityReader.Require{T}"/>).</summary>
    /// <exception cref="InvalidOperationException">The manager doesn't exist.</exception>
    public static TerrainReader Create() => new(CityReader.Require<TerrainManager>());

    /// <summary>
    /// The 16 m grid of the raw and final heights and of the water cells: <c>RAW_RESOLUTION</c> cells of
    /// <c>RAW_CELL_SIZE</c> metres, centred on the origin, so 1081 points from −8640 to 8640 m.
    /// </summary>
    public static GridGeometry HeightGrid => CenteredGrid(TerrainManager.RAW_RESOLUTION, TerrainManager.RAW_CELL_SIZE);

    /// <summary>
    /// The 4 m lattice of the detail heights: <c>DETAIL_RESOLUTION</c> cells of <c>DETAIL_CELL_SIZE</c> metres per
    /// tile, over the <c>PATCH_RESOLUTION</c> × <c>PATCH_RESOLUTION</c> tiles, so 4321 points from −8640 to 8640 m.
    /// </summary>
    public static GridGeometry DetailGrid =>
        CenteredGrid(TerrainManager.DETAIL_RESOLUTION * TerrainManager.PATCH_RESOLUTION, TerrainManager.DETAIL_CELL_SIZE);

    /// <summary>
    /// Reads the raw and final heights and the detail heights of every tile that has them. Simulation thread only.
    /// </summary>
    /// <param name="problems">Where to add problems that only lose part of the data, such as a damaged detail tile.</param>
    /// <exception cref="InvalidOperationException">The game's arrays don't have the expected sizes.</exception>
    public TerrainSectionContent ReadTerrain(IList<string> problems)
    {
        if (TerrainManager.PATCH_RESOLUTION != TileCoordinate.GridSize)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "The terrain has {0} × {0} patches; the dump expects {1} × {1} tiles.",
                TerrainManager.PATCH_RESOLUTION,
                TileCoordinate.GridSize));
        }

        GridGeometry grid = HeightGrid;
        ushort[] raw = (ushort[])RequireLength(_terrain.RawHeights, grid.PointCount, "raw heights").Clone();
        ushort[] final = (ushort[])RequireLength(_terrain.FinalHeights, grid.PointCount, "final heights").Clone();
        int tileResolution = TerrainManager.DETAIL_RESOLUTION + 1;
        var content = new TerrainSectionContent(grid, raw, final, DetailGrid, tileResolution);

        // A patch with detail data has m_simDetailIndex > 0; its heights are block number index − 1 of
        // m_detailHeights, tileResolution² values row by row (docs/research/cs1-save-and-world.md, Terrain).
        TerrainPatch[] patches = _terrain.m_patches;
        ushort[] detail = _terrain.m_detailHeights;
        int tilePoints = tileResolution * tileResolution;
        for (int z = 0; z < TileCoordinate.GridSize; z++)
        {
            for (int x = 0; x < TileCoordinate.GridSize; x++)
            {
                TerrainPatch? patch = patches is null || patches.Length <= (z * TileCoordinate.GridSize) + x
                    ? null
                    : patches[(z * TileCoordinate.GridSize) + x];
                int index = patch?.m_simDetailIndex ?? 0;
                if (index <= 0)
                {
                    continue;
                }

                long start = (long)(index - 1) * tilePoints;
                var tile = new TileCoordinate(x, z);
                if (detail is null || start + tilePoints > detail.Length)
                {
                    problems.Add("Tile " + tile + " has detail heights the exporter couldn't find, so its 4 m heights are missing.");
                    continue;
                }

                ushort[] heights = new ushort[tilePoints];
                Array.Copy(detail, (int)start, heights, 0, tilePoints);
                content.DetailTiles.Add(new TerrainDetailTile(tile, heights));
            }
        }

        return content;
    }

    /// <summary>
    /// Reads the sea level, the water simulation's cells and the water sources. Simulation thread only. The cells
    /// are copied under the water simulation's read lock, which the game's own samplers use too.
    /// </summary>
    /// <exception cref="InvalidOperationException">There is no water simulation, or its arrays don't have the expected sizes.</exception>
    public WaterSectionContent ReadWater()
    {
        WaterSimulation water = _terrain.WaterSimulation
            ?? throw new InvalidOperationException("The terrain has no water simulation.");
        GridGeometry grid = HeightGrid;
        int points = grid.PointCount;

        // The water simulation's ground is the terrain's block heights: the same array is its height buffer.
        ushort[] ground = (ushort[])RequireLength(_terrain.BlockHeights, points, "block heights").Clone();
        ushort[] depth = new ushort[points];
        ushort[] pollution = new ushort[points];
        short[] velocityX = new short[points];
        short[] velocityZ = new short[points];
        WaterSimulation.Cell[] cells = water.BeginRead();
        try
        {
            if (cells is null || cells.Length < points)
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "The water simulation has {0} cells; the terrain grid has {1} points.",
                    cells?.Length ?? 0,
                    points));
            }

            for (int i = 0; i < points; i++)
            {
                depth[i] = cells[i].m_height;
                pollution[i] = cells[i].m_pollution;
                velocityX[i] = cells[i].m_velocityX;
                velocityZ[i] = cells[i].m_velocityZ;
            }
        }
        finally
        {
            water.EndRead();
        }

        var content = new WaterSectionContent(water.m_currentSeaLevel, grid, ground, depth, pollution, velocityX, velocityZ);
        ReadSources(water, content.Sources);
        return content;
    }

    private static void ReadSources(WaterSimulation water, IList<WaterSourceRecord> target)
    {
        // The game locks the list whenever it reads or changes it; an entry of type 0 is a free slot. A source's id is
        // its index plus 1.
        FastList<WaterSource> sources = water.m_waterSources;
        if (sources is null)
        {
            return;
        }

        lock (sources)
        {
            for (int i = 0; i < sources.m_size; i++)
            {
                WaterSource source = sources.m_buffer[i];
                if (source.m_type == WaterSource.TYPE_NONE)
                {
                    continue;
                }

                target.Add(new WaterSourceRecord
                {
                    Id = (uint)(i + 1),
                    Type = source.m_type,
                    Target = source.m_target,
                    InputX = source.m_inputPosition.x,
                    InputY = source.m_inputPosition.y,
                    InputZ = source.m_inputPosition.z,
                    OutputX = source.m_outputPosition.x,
                    OutputY = source.m_outputPosition.y,
                    OutputZ = source.m_outputPosition.z,
                    Water = source.m_water,
                    Pollution = source.m_pollution,
                    InputRate = source.m_inputRate,
                    OutputRate = source.m_outputRate,
                    Flow = source.m_flow,
                });
            }
        }
    }

    private static GridGeometry CenteredGrid(int cells, float spacing)
    {
        float origin = -cells * spacing / 2;
        return new GridGeometry(origin, origin, spacing, cells + 1);
    }

    private static ushort[] RequireLength(ushort[]? values, int expected, string what)
    {
        if (values is null || values.Length != expected)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "The terrain's {0} have {1} values; expected {2}.",
                what,
                values?.Length ?? 0,
                expected));
        }

        return values;
    }
}
