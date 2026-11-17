using System;
using System.IO;
using CsToBeamNG.Converter.BeamNG.Terrain;
using Xunit;

namespace CsToBeamNG.Converter.Tests.BeamNG;

public sealed class TerFileTests
{
    [Fact]
    public void WritesTheVersion9Layout()
    {
        var terrain = new TerrainData(2, ["a", "bc"]);
        terrain.SetHeight(0, 0, 0x0102);
        terrain.SetHeight(1, 0, 0x0304);
        terrain.SetHeight(0, 1, 0x0506);
        terrain.SetHeight(1, 1, 0xFFFF);
        terrain.SetMaterial(1, 0, 1);
        terrain.SetMaterial(0, 1, 1);
        terrain.SetMaterial(1, 1, TerrainData.HoleMaterialIndex);

        byte[] bytes = Write(terrain);

        byte[] expected =
        [
            9, // version
            2, 0, 0, 0, // size
            0x02, 0x01, 0x04, 0x03, 0x06, 0x05, 0xFF, 0xFF, // heights, row by row, little-endian
            0, 1, 1, 255, // materials
            2, 0, 0, 0, // material count
            1, (byte)'a', 2, (byte)'b', (byte)'c', // names
        ];
        Assert.Equal(expected, bytes);
        Assert.Equal(expected.Length, TerFile.GetLength(terrain));
    }

    [Fact]
    public void HasTheLengthOfTheGamesOwnFiles()
    {
        // The official template level's theTerrain.ter: 1024 samples and four materials, 3,145,775 bytes.
        var terrain = new TerrainData(1024, ["Grass", "BeachSand", "dirt_rocky_large", "rock"]);

        Assert.Equal(3_145_775, TerFile.GetLength(terrain));
        Assert.Equal(3_145_775, Write(terrain).Length);
    }

    [Fact]
    public void ReadsWhatItWrites()
    {
        var terrain = new TerrainData(64, ["grass", "asphalt", "rock"]);
        var random = new Random(1234);
        for (int i = 0; i < terrain.Heights.Length; i++)
        {
            terrain.Heights[i] = (ushort)random.Next(ushort.MaxValue + 1);
            terrain.Materials[i] = (byte)random.Next(3);
        }

        TerrainData read = TerFile.Read(new MemoryStream(Write(terrain)));

        Assert.Equal(terrain.Size, read.Size);
        Assert.Equal(terrain.MaterialNames, read.MaterialNames);
        Assert.True(terrain.Heights.SequenceEqual(read.Heights));
        Assert.True(terrain.Materials.SequenceEqual(read.Materials));
    }

    [Fact]
    public void IndexesSamplesRowByRow()
    {
        var terrain = new TerrainData(4, ["a"]);
        terrain.SetHeight(1, 0, 10);
        terrain.SetHeight(0, 1, 20);

        Assert.Equal(10, terrain.Heights[1]);
        Assert.Equal(20, terrain.Heights[4]);
        Assert.Equal(20, terrain.GetHeight(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => terrain.GetHeight(4, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => terrain.SetHeight(0, -1, 1));
    }

    [Fact]
    public void RejectsWrongVersionsAndTruncatedFiles()
    {
        byte[] bytes = Write(new TerrainData(2, ["a"]));

        byte[] otherVersion = (byte[])bytes.Clone();
        otherVersion[0] = 8;
        Assert.Throws<InvalidDataException>(() => TerFile.Read(new MemoryStream(otherVersion)));
        Assert.Throws<InvalidDataException>(() => TerFile.Read(new MemoryStream(bytes[..^1])));
        Assert.Throws<InvalidDataException>(() => TerFile.Read(new MemoryStream(bytes[..10])));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(1000)]
    [InlineData(16384)]
    public void RequiresAPowerOfTwoSize(int size) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainData(size, ["a"]));

    [Fact]
    public void ChecksMaterials()
    {
        Assert.Throws<ArgumentException>(() => new TerrainData(2, []));
        Assert.Throws<ArgumentException>(() => new TerrainData(2, ["a", "a"]));
        Assert.Throws<ArgumentException>(() => new TerrainData(2, ["gräs"]));
        Assert.Throws<ArgumentException>(() => new TerrainData(2, [new string('a', 256)]));
        Assert.Throws<ArgumentException>(() => new TerrainData(2, new string[255]));

        var terrain = new TerrainData(2, ["a", "b"]);
        terrain.SetMaterial(0, 0, TerrainData.HoleMaterialIndex);
        Assert.Throws<ArgumentOutOfRangeException>(() => terrain.SetMaterial(0, 0, 2));
    }

    private static byte[] Write(TerrainData terrain)
    {
        using var stream = new MemoryStream();
        TerFile.Write(stream, terrain);
        return stream.ToArray();
    }
}
