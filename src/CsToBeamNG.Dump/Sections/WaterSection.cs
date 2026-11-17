namespace CsToBeamNG.Dump.Sections;

/// <summary>
/// The <c>water</c> section: the sea level, the water simulation's cells on the 16 m grid, and the water sources
/// (docs/dump-format.md, <c>water</c>; docs/research/cs1-save-and-world.md, Water). <see cref="WaterSectionWriter"/>
/// writes it; <c>WaterSectionReader</c> reads it in the .NET 10 build.
/// </summary>
public static class WaterSection
{
    /// <summary>The section's name.</summary>
    public const string Name = "water";

    /// <summary>The section's major version.</summary>
    public const int MajorVersion = 0;

    /// <summary>The section's minor version.</summary>
    public const int MinorVersion = 1;

    /// <summary>The JSON file with the sea level and the cell grid's geometry.</summary>
    public const string InfoFileName = "water.json";

    /// <summary>The cells, one per grid point, row by row.</summary>
    public const string CellsFileName = "cells.bin";

    /// <summary>The water sources.</summary>
    public const string SourcesFileName = "sources.bin";

    /// <summary>The record type of <see cref="CellsFileName"/>.</summary>
    public const string CellRecordType = "waterCell";

    /// <summary>The size of a <see cref="CellRecordType"/> record in bytes.</summary>
    public const int CellRecordSize = 10;

    /// <summary>The record type of <see cref="SourcesFileName"/>.</summary>
    public const string SourceRecordType = "waterSource";

    /// <summary>The size of a <see cref="SourceRecordType"/> record in bytes.</summary>
    public const int SourceRecordSize = 52;

    /// <summary>The height of one unit of a cell's ground and depth and of a source's target, in metres: 1/64 m.</summary>
    public const float MetersPerHeightUnit = 1f / 64;

    /// <summary>The section's version.</summary>
    public static DumpVersion Version => new(MajorVersion, MinorVersion);
}
