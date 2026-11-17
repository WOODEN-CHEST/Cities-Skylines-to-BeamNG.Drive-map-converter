using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CsToBeamNG.Converter.Development.HeightProbe;
using CsToBeamNG.Converter.Terrain;
using CsToBeamNG.Converter.Tests.Fixtures;
using CsToBeamNG.Dump.Manifest;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Development;

public sealed class HeightProbeCheckTests : IDisposable
{
    // Final heights rise towards the east; tile (4, 4) has detail heights 3 m above them.
    private static readonly CityTerrain _terrain = new(TestTerrain.Create((x, z) => 20, (x, z) => 30 + (x / 8))
        .WithDetail(new TileCoordinate(4, 4), (x, z) => 33 + (x / 8)));

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void MatchesTheGamesSamplers()
    {
        HeightProbeFile probe = Read(Probe(differingRaw: 0, (-40, 10, 0), (2, -3, 0), (6, 6, 0)));

        IReadOnlyList<HeightProbeComparison> comparisons = HeightProbeCheck.Compare(_terrain, probe);

        Assert.Equal(["ground: SampleGround vs SampleDetailHeight", "final heights vs SampleFinalHeight", "raw heights vs SampleRawHeight"], comparisons.Select(c => c.Name));
        Assert.All(comparisons, c => Assert.True(c.IsMatch, c.Name));
        Assert.All(comparisons, c => Assert.Equal(3, c.Count));
    }

    [Fact]
    public void FindsTheWorstMismatch()
    {
        HeightProbeFile probe = Read(Probe(differingRaw: 0, (-40, 10, 0), (2, -3, 0.01), (6, 6, 0.002)));

        HeightProbeComparison ground = HeightProbeCheck.Compare(_terrain, probe)[0];

        Assert.False(ground.IsMatch);
        Assert.Equal(0.01, ground.MaxErrorMeters, 9);
        Assert.Equal(0.004, ground.MeanErrorMeters, 9);
        Assert.Equal(1, ground.Worst?.Id);
    }

    [Fact]
    public void LeavesOutTheRawComparisonWhenTheGamesCopyDiffered()
    {
        HeightProbeFile probe = Read(Probe(differingRaw: 12, (0, 0, 0)));

        Assert.Equal(2, HeightProbeCheck.Compare(_terrain, probe).Count);
        Assert.Equal(new HeightArrayComparison("rawVsRaw2", 361, 12, 0.5, -0.25), probe.Arrays.Single());
    }

    [Fact]
    public void SummarisesTheObjectsAndFormatsAReport()
    {
        using var scope = new CultureScope("lv-LV");
        HeightProbeFile probe = Read(Probe(differingRaw: 0, (-40, 10, 0), (2, -3, 0.5)));

        IReadOnlyList<ObjectHeightSummary> objects = HeightProbeCheck.SummarizeObjects(probe);
        string report = HeightProbeCheck.FormatReport(HeightProbeCheck.Compare(_terrain, probe), objects);

        ObjectHeightSummary node = Assert.Single(objects);
        Assert.Equal(("node", 2, 1), (node.Kind, node.Count, node.WithinTwoCentimetres));
        Assert.Contains("  node            2: above the ground  -0.125 (1 within 2 cm)", report, StringComparison.Ordinal);
        Assert.Contains("max  500.000 mm", report, StringComparison.Ordinal);
        Assert.Contains("DIFFERENT (worst: node 1 at x 2, z -3)", report, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnotherFormat()
    {
        string path = _temp.PathOf("other.json");
        File.WriteAllText(path, """{ "format": "cstobeamng-geometry-probe", "version": 1 }""");

        Assert.Throws<InvalidDataException>(() => HeightProbeFile.Read(path));
    }

    private HeightProbeFile Read(string json)
    {
        string path = _temp.PathOf("heights-probe.json");
        File.WriteAllText(path, json);
        return HeightProbeFile.Read(path);
    }

    // A probe with one node sample per position: the game's heights are the converter's, plus an error on the ground.
    // The nodes stand 0.25 m above the converter's ground, minus the error.
    private static string Probe(int differingRaw, params (double X, double Z, double GroundError)[] positions)
    {
        var samples = new List<Dictionary<string, object>>();
        for (int i = 0; i < positions.Length; i++)
        {
            (double x, double z, double error) = positions[i];
            double ground = _terrain.SampleGround(x, z);
            samples.Add(new Dictionary<string, object>
            {
                ["kind"] = "node",
                ["id"] = i,
                ["label"] = "Basic Road",
                ["flags"] = "00004001",
                ["x"] = x,
                ["y"] = i == 0 ? ground : ground + 0.25,
                ["z"] = z,
                ["detail"] = ground + error,
                ["detailSmooth"] = ground,
                ["final"] = _terrain.FinalHeights.Sample(x, z),
                ["finalSmooth"] = 0,
                ["raw"] = _terrain.RawHeights.Sample(x, z),
                ["rawSmooth"] = 20,
                ["originalRawSmooth"] = 20,
                ["blockSmooth"] = 0,
                ["surface"] = 0,
            });
        }

        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["format"] = HeightProbeFile.FormatName,
            ["version"] = HeightProbeFile.FormatVersion,
            ["game"] = "1.21.1-f9",
            ["arrays"] = new Dictionary<string, object>
            {
                ["rawVsRaw2"] = new Dictionary<string, object> { ["values"] = 361, ["differing"] = differingRaw, ["maxAbove"] = 0.5, ["maxBelow"] = -0.25 },
                ["detailPatchCount"] = 1,
            },
            ["samples"] = samples,
        });
    }
}
