using System;
using System.Collections.Generic;
using System.Numerics;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Json;
using CsToBeamNG.Dump.Manifest;
using CsToBeamNG.Dump.Records;
using CsToBeamNG.Dump.Sections;

namespace CsToBeamNG.Converter.Tests.Fixtures;

/// <summary>
/// Builds small synthetic dumps with the real <see cref="DumpWriter"/>, for loader and render tests. They hold only
/// made-up layout data (agents/conventions.md, Tests). Write each instance once.
/// </summary>
internal sealed class SyntheticDump
{
    private readonly List<Action<DumpWriter>> _sections = [];

    /// <summary>The manifest to write; tests may change it before <see cref="WriteTo"/>.</summary>
    public DumpManifest Manifest { get; } = CreateManifest();

    /// <summary>Whether the manifest says the export finished.</summary>
    public bool Complete { get; set; } = true;

    /// <summary>A manifest with the required fields and a few counts, but no tiles or sections.</summary>
    public static DumpManifest CreateManifest()
    {
        var manifest = new DumpManifest
        {
            CreatedUtc = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc),
            Exporter = new ExporterInfo("CsToBeamNG.Exporter", "0.1.0-test"),
            Game = new GameInfo { Version = "1.21.1-f9" },
            City = new CityInfo { Name = "Synthetic City", MapName = "Test Map", Environment = "Sunny" },
        };
        manifest.Counts["buildings"] = 12;
        manifest.Counts["netNodes"] = 34;
        return manifest;
    }

    public SyntheticDump WithUnlockedTiles(params TileCoordinate[] tiles)
    {
        foreach (TileCoordinate tile in tiles)
        {
            Manifest.City.UnlockedTiles.Add(tile);
        }

        return this;
    }

    /// <summary>Adds a <see cref="TestPoints"/> section with one record per point.</summary>
    public SyntheticDump WithPoints(params Vector3[] points) =>
        WithPoints(TestPoints.SectionName, TestPoints.Version, TestPoints.RecordType, points);

    /// <summary>Adds a points section with a chosen name, version and record type, to test mismatches.</summary>
    public SyntheticDump WithPoints(string sectionName, DumpVersion version, string recordType, params Vector3[] points)
    {
        _sections.Add(dump =>
        {
            SectionWriter section = dump.BeginSection(sectionName, version);
            using (RecordFileWriter records = section.CreateRecordFile(TestPoints.FileName, recordType, TestPoints.RecordSize))
            {
                foreach (Vector3 point in points)
                {
                    records.BeginRecord();
                    records.WriteSingle(point.X);
                    records.WriteSingle(point.Y);
                    records.WriteSingle(point.Z);
                    records.EndRecord();
                }

                records.Finish();
            }

            section.RecordCount = points.Length;
            section.Complete();
        });
        return this;
    }

    /// <summary>Adds a <c>terrain</c> section, written by the real section writer.</summary>
    public SyntheticDump WithTerrain(TerrainSectionContent terrain)
    {
        _sections.Add(dump => TerrainSectionWriter.Write(dump, terrain));
        return this;
    }

    /// <summary>Adds a <c>water</c> section, written by the real section writer.</summary>
    public SyntheticDump WithWater(WaterSectionContent water)
    {
        _sections.Add(dump => WaterSectionWriter.Write(dump, water));
        return this;
    }

    /// <summary>
    /// Adds the network sections, written by the real section writers; a <see langword="null"/> content leaves its
    /// section out.
    /// </summary>
    public SyntheticDump WithNetwork(NetPrefabsSectionContent? prefabs, NetNodesSectionContent? nodes, NetSegmentsSectionContent? segments, NetLanesSectionContent? lanes)
    {
        _sections.Add(dump =>
        {
            if (prefabs is not null)
            {
                NetPrefabsSectionWriter.Write(dump, prefabs);
            }

            if (nodes is not null)
            {
                NetNodesSectionWriter.Write(dump, nodes);
            }

            if (segments is not null)
            {
                NetSegmentsSectionWriter.Write(dump, segments);
            }

            if (lanes is not null)
            {
                NetLanesSectionWriter.Write(dump, lanes);
            }
        });
        return this;
    }

    /// <summary>
    /// Adds the sections of buildings, props and trees, written by the real section writers; a <see langword="null"/>
    /// content leaves its section out.
    /// </summary>
    public SyntheticDump WithObjects(
        BuildingPrefabsSectionContent? buildingPrefabs,
        BuildingsSectionContent? buildings,
        PropPrefabsSectionContent? propPrefabs,
        PropsSectionContent? props,
        TreePrefabsSectionContent? treePrefabs,
        TreesSectionContent? trees)
    {
        _sections.Add(dump =>
        {
            if (buildingPrefabs is not null)
            {
                BuildingPrefabsSectionWriter.Write(dump, buildingPrefabs);
            }

            if (buildings is not null)
            {
                BuildingsSectionWriter.Write(dump, buildings);
            }

            if (propPrefabs is not null)
            {
                PropPrefabsSectionWriter.Write(dump, propPrefabs);
            }

            if (props is not null)
            {
                PropsSectionWriter.Write(dump, props);
            }

            if (treePrefabs is not null)
            {
                TreePrefabsSectionWriter.Write(dump, treePrefabs);
            }

            if (trees is not null)
            {
                TreesSectionWriter.Write(dump, trees);
            }
        });
        return this;
    }

    /// <summary>Adds a section that the test writes itself, such as records that the section writers would reject.</summary>
    public SyntheticDump WithCustomSection(Action<DumpWriter> write)
    {
        _sections.Add(write);
        return this;
    }

    /// <summary>Adds a section with one small JSON file, which no loader knows.</summary>
    public SyntheticDump WithOpaqueSection(string name, DumpVersion version)
    {
        _sections.Add(dump =>
        {
            SectionWriter section = dump.BeginSection(name, version);
            using (JsonWriter json = section.CreateJsonFile("data.json"))
            {
                json.WriteStartObject();
                json.WriteString("note", "synthetic");
                json.WriteEndObject();
            }

            section.Complete();
        });
        return this;
    }

    /// <summary>Writes the dump into a new or empty folder, as the exporter does: manifest first, sections, manifest again.</summary>
    /// <returns>The dump folder's full path.</returns>
    public string WriteTo(string folder)
    {
        Manifest.Complete = false;
        DumpWriter writer = DumpWriter.Create(folder, Manifest);
        writer.WriteManifest();
        foreach (Action<DumpWriter> addSection in _sections)
        {
            addSection(writer);
        }

        Manifest.Complete = Complete;
        writer.WriteManifest();
        return writer.Folder;
    }
}
