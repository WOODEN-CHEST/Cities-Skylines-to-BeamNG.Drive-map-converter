using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using CsToBeamNG.Converter.BeamNG.Level;
using CsToBeamNG.Converter.BeamNG.Scene;
using Xunit;

namespace CsToBeamNG.Converter.Tests.BeamNG;

public sealed class WaterObjectsTests
{
    [Fact]
    public void WritesASeaPlane()
    {
        JsonElement plane = WriteOne(new WaterPlane { Key = "water/sea", Name = "sea", Height = 59.6875f, Style = WaterStyles.Sea() });

        Assert.Equal("WaterPlane", plane.GetProperty("class").GetString());
        Assert.Equal([0.0, 0, 59.6875], plane.GetProperty("position").EnumerateArray().Select(v => v.GetDouble()));
        Assert.Equal(WaterStyles.ReflectionCubemap, plane.GetProperty("cubemap").GetString());
        Assert.Equal(3, plane.GetProperty("Ripples (texture animation)").GetArrayLength());
    }

    [Fact]
    public void WritesAWaterBlockTwiceAsHighAsItsDepth()
    {
        JsonElement block = WriteOne(new WaterBlock
        {
            Key = "water/lake/1/1",
            Name = "lake_1_1",
            SurfaceCentre = new Vector3(100, -50, 108),
            Width = 64,
            Length = 32,
            Depth = 20,
            GridElementSize = 4,
            Style = WaterStyles.Lake(),
        });

        Assert.Equal("WaterBlock", block.GetProperty("class").GetString());
        Assert.Equal([100.0, -50, 108], block.GetProperty("position").EnumerateArray().Select(v => v.GetDouble()));
        Assert.Equal([64.0, 32, 40], block.GetProperty("scale").EnumerateArray().Select(v => v.GetDouble()));
        Assert.Equal(4, block.GetProperty("gridElementSize").GetDouble());
        Assert.Equal([1.0, 0, 0, 0, 1, 0, 0, 0, 1], block.GetProperty("rotationMatrix").EnumerateArray().Select(v => v.GetDouble()));
    }

    [Fact]
    public void RejectsAWaterBlockWithoutSize()
    {
        var block = new WaterBlock { Key = "w", SurfaceCentre = Vector3.Zero, Width = 0, Length = 1, Depth = 1, Style = WaterStyles.Lake() };

        Assert.Throws<InvalidOperationException>(() => Write(block));
    }

    [Fact]
    public void StylesUseOnlyTheGamesSharedTextures()
    {
        foreach (WaterStyle style in new[] { WaterStyles.Sea(), WaterStyles.Lake() })
        {
            List<string> textures = [.. style.Fields.Entries.Where(f => f.Key.EndsWith("Tex", StringComparison.Ordinal)).Select(f => f.Value!.GetValue<string>())];
            Assert.NotEmpty(textures);
            Assert.All(textures, t => Assert.StartsWith("/assets/materials/tileable/water/", t, StringComparison.Ordinal));
        }
    }

    private static JsonElement WriteOne(SceneObject water)
    {
        string line = Write(water).Split('\n', StringSplitOptions.RemoveEmptyEntries).Single();
        return JsonDocument.Parse(line).RootElement.Clone();
    }

    private static string Write(SceneObject water)
    {
        var root = new SimGroup(SceneTreeWriter.RootGroupName);
        root.Children.Add(water);
        return SceneTreeWriter.CreateFiles("cstobeamng_test", root).Single(f => f.Path.Value.EndsWith("MissionGroup/items.level.json", StringComparison.Ordinal)).Text();
    }
}
