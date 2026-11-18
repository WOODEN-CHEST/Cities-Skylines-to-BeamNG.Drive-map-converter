using System;
using CsToBeamNG.Converter.Planning;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Planning;

public sealed class TerrainHeightRangeTests
{
    [Fact]
    public void LeavesAMetreOnBothSidesInWholeMetres()
    {
        TerrainHeightRange range = TerrainHeightRange.Plan(0.83, 638.11);

        Assert.Equal(-1, range.ZOffset);
        Assert.Equal(641, range.MaxHeight);
        Assert.Equal(641 / 65536.0, range.StepMeters, 12);
    }

    [Fact]
    public void StoresHeightsWithinHalfAStep()
    {
        TerrainHeightRange range = TerrainHeightRange.Plan(30.5, 163.9);

        foreach (double height in new[] { 30.5, 44.6875, 100.123456, 163.9 })
        {
            Assert.Equal(height, range.ToMeters(range.ToStored(height)), range.StepMeters / 2);
        }
    }

    [Fact]
    public void RejectsInvalidRanges()
    {
        Assert.Throws<ArgumentException>(() => TerrainHeightRange.Plan(10, 5));
        Assert.Throws<ArgumentException>(() => TerrainHeightRange.Plan(double.NaN, 5));
    }
}
