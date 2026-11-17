#if NET
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using CsToBeamNG.Dump.Json;
using CsToBeamNG.Dump.Records;

namespace CsToBeamNG.Dump.Sections;

/// <summary>Reads a <c>water</c> section written by <see cref="WaterSectionWriter"/>. Only in the .NET 10 build.</summary>
public static class WaterSectionReader
{
    /// <summary>Reads the section.</summary>
    /// <param name="section">The section; its major version must be <see cref="WaterSection.MajorVersion"/>.</param>
    /// <exception cref="DumpException">A file is missing, damaged or inconsistent with <c>water.json</c>.</exception>
    public static WaterSectionContent Read(DumpSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        string infoPath = section.GetFilePath(WaterSection.InfoFileName);
        using JsonDocument document = section.ReadJson(WaterSection.InfoFileName);
        var json = new JsonFieldReader(infoPath);
        JsonElement root = document.RootElement;
        json.RequireObject(root, "$");
        float seaLevel = json.RequiredSingle(root, "seaLevel", "$");
        GridGeometry grid = GridGeometry.Read(json, root, "grid", "$");

        CellLayout[] cells;
        using (RecordFileReader records = section.OpenRecordFile(WaterSection.CellsFileName, WaterSection.CellRecordType, WaterSection.CellRecordSize))
        {
            if (records.RecordCount != grid.PointCount)
            {
                throw new DumpException(
                    DumpErrorKind.Corrupt,
                    string.Create(CultureInfo.InvariantCulture, $"'{records.SourceName}' has {records.RecordCount} cells, but water.json describes {grid.PointCount}."));
            }

            cells = records.ReadAllRecords<CellLayout>();
        }

        var content = new WaterSectionContent(
            seaLevel,
            grid,
            new ushort[cells.Length],
            new ushort[cells.Length],
            new ushort[cells.Length],
            new short[cells.Length],
            new short[cells.Length]);
        for (int i = 0; i < cells.Length; i++)
        {
            content.Ground[i] = cells[i].Ground;
            content.Depth[i] = cells[i].Depth;
            content.Pollution[i] = cells[i].Pollution;
            content.VelocityX[i] = cells[i].VelocityX;
            content.VelocityZ[i] = cells[i].VelocityZ;
        }

        using (RecordFileReader records = section.OpenRecordFile(WaterSection.SourcesFileName, WaterSection.SourceRecordType, WaterSection.SourceRecordSize))
        {
            while (records.MoveNext())
            {
                content.Sources.Add(new WaterSourceRecord
                {
                    Id = records.ReadUInt32(),
                    Type = records.ReadUInt16(),
                    Target = records.ReadUInt16(),
                    InputX = records.ReadSingle(),
                    InputY = records.ReadSingle(),
                    InputZ = records.ReadSingle(),
                    OutputX = records.ReadSingle(),
                    OutputY = records.ReadSingle(),
                    OutputZ = records.ReadSingle(),
                    Water = records.ReadUInt32(),
                    Pollution = records.ReadUInt32(),
                    InputRate = records.ReadUInt32(),
                    OutputRate = records.ReadUInt32(),
                    Flow = records.ReadUInt32(),
                });
            }
        }

        return content;
    }

    // The memory layout of a waterCell record: five 2-byte fields, so no padding. ReadAllRecords fills the fields;
    // the constructor only exists so the compiler sees them assigned.
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private readonly struct CellLayout(ushort ground, ushort depth, ushort pollution, short velocityX, short velocityZ)
    {
        public readonly ushort Ground = ground;
        public readonly ushort Depth = depth;
        public readonly ushort Pollution = pollution;
        public readonly short VelocityX = velocityX;
        public readonly short VelocityZ = velocityZ;
    }
}
#endif
