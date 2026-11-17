using System;
using CsToBeamNG.Converter.Networks;
using CsToBeamNG.Dump.Sections;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Networks;

public sealed class NetGeometryTests
{
    // A straight curve from (0, 60, 0) to (30, 64, -40) with evenly spaced control points.
    private static readonly DumpBezier3 _straight = new(
        new DumpVector3(0, 60, 0), new DumpVector3(10, 61, -10), new DumpVector3(20, 62, -20), new DumpVector3(30, 63, -30));

    // The usual cubic approximation of a quarter circle of radius 100 around the origin, from +X to +Z.
    private static readonly DumpBezier3 _quarter = new(
        new DumpVector3(100, 0, 0), new DumpVector3(100, 0, 55.228475f), new DumpVector3(55.228475f, 0, 100), new DumpVector3(0, 0, 100));

    [Fact]
    public void EvaluatesTheCurve()
    {
        Assert.Equal((0.0, 60.0, 0.0), NetGeometry.Position(_straight, 0));
        Assert.Equal((30.0, 63.0, -30.0), NetGeometry.Position(_straight, 1));
        Assert.Equal((15.0, 61.5, -15.0), NetGeometry.Position(_straight, 0.5));
        Assert.Equal((30.0, 3.0, -30.0), NetGeometry.Derivative(_straight, 0.25));
        (double x, _, double z) = NetGeometry.Derivative(_quarter, 0);
        Assert.Equal((0.0, 3 * (double)55.228475f), (x, z));
    }

    [Fact]
    public void MeasuresLengths()
    {
        double straight = Math.Sqrt((30 * 30) + (3 * 3) + (30 * 30));
        Assert.Equal(straight, NetGeometry.ArcLength(_straight), 9);
        Assert.Equal(straight, NetGeometry.ControlPolygonLength(_straight), 9);

        // The quarter circle is 157.08 m long; its control polygon is longer, as the game's lane lengths are on curves.
        Assert.Equal(Math.PI * 50, NetGeometry.ArcLength(_quarter), 1);
        Assert.Equal(55.228475 + (Math.Sqrt(2) * 44.771525) + 55.228475, NetGeometry.ControlPolygonLength(_quarter), 4);
        Assert.Equal(Math.Sqrt(30 * 30 * 2), NetGeometry.HorizontalDistance(_straight.A, _straight.D), 9);
        Assert.Equal(5, NetGeometry.Distance(new DumpVector3(0, 0, 0), new DumpVector3(3, 4, 0)), 12);
    }
}
