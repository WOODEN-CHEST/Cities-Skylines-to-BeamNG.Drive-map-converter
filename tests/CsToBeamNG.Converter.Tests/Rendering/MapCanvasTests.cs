using System;
using System.Collections.Generic;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.Rendering;
using CsToBeamNG.Converter.World;
using Xunit;

namespace CsToBeamNG.Converter.Tests.Rendering;

public sealed class MapCanvasTests
{
    private static readonly Rgb _red = new(255, 0, 0);
    private static readonly Rgb _blue = new(0, 0, 255);

    // 100 m × 100 m at 10 px: pixel (i, j) covers x 10i to 10i + 10 and z 90 − 10j to 100 − 10j.
    private readonly MapCanvas _canvas = new(new RgbImage(10, 10, default), new RenderView(new MapRect(0, 0, 100, 100), 10));

    [Fact]
    public void FillsThePixelsWhoseCentresAreInside()
    {
        _canvas.FillRect(new MapRect(0, 0, 30, 20), _red);

        Assert.Equal([(0, 8), (1, 8), (2, 8), (0, 9), (1, 9), (2, 9)], Find(_red));
    }

    [Fact]
    public void FillsAdjacentRectanglesWithoutGapOrOverlap()
    {
        _canvas.FillRect(new MapRect(0, 0, 34, 100), _red);
        _canvas.FillRect(new MapRect(34, 0, 61, 100), _blue);

        for (int y = 0; y < 10; y++)
        {
            Assert.Equal([_red, _red, _red, _blue, _blue, _blue, default, default, default, default], Row(y));
        }
    }

    [Fact]
    public void BlendsOverWhatIsThere()
    {
        _canvas.FillRect(new MapRect(0, 0, 100, 100), _blue);

        _canvas.BlendRect(new MapRect(40, 40, 60, 60), _red, 255);
        _canvas.BlendRect(new MapRect(0, 0, 10, 10), _red, 128);

        Assert.Equal([(4, 4), (5, 4), (4, 5), (5, 5)], Find(_red));
        Assert.Equal(_blue.Blend(_red, 128), _canvas.Image[0, 9]);
    }

    [Fact]
    public void OutlinesAlongTheInside()
    {
        _canvas.StrokeRect(new MapRect(0, 0, 100, 100), _red, 2);

        Assert.Equal(_red, _canvas.Image[0, 0]);
        Assert.Equal(_red, _canvas.Image[1, 1]);
        Assert.Equal(_red, _canvas.Image[9, 5]);
        Assert.Equal(_red, _canvas.Image[5, 8]);
        Assert.Equal(default, _canvas.Image[2, 2]);
        Assert.Equal(default, _canvas.Image[7, 7]);
        Assert.Equal(64, Find(_red).Count);
    }

    [Fact]
    public void OutlinesOnlyTheEdgesInsideTheImage()
    {
        _canvas.StrokeRect(new MapRect(-50, -50, 55, 55), _red, 1);

        var expected = new List<(int, int)>();
        for (int y = 4; y < 10; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                if (y == 4 || x == 4)
                {
                    expected.Add((x, y));
                }
            }
        }

        Assert.Equal(expected, Find(_red));
    }

    [Fact]
    public void SkipsOutlinesOfRectanglesSmallerThanAPixel()
    {
        _canvas.StrokeRect(new MapRect(51, 51, 54, 54), _red, 1);

        Assert.Empty(Find(_red));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(MapCanvas.MaxLineWidthPixels + 1)]
    public void RejectsUnusableOutlineWidths(int width) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _canvas.StrokeRect(new MapRect(0, 0, 10, 10), _red, width));

    [Fact]
    public void DrawsLinesInWorldCoordinates()
    {
        _canvas.DrawLine(5, 55, 95, 55, _red);
        _canvas.DrawLine(0, 100, 100, 0, _blue);

        Assert.Equal([_red, _red, _red, _red, _blue, _red, _red, _red, _red, _red], Row(4));
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(_blue, _canvas.Image[i, i]);
        }

        Assert.Equal(19, Find(_red).Count + Find(_blue).Count);
    }

    [Fact]
    public void CutsLinesAtTheImageEdge()
    {
        _canvas.DrawLine(-1e9, 55, 1e9, 55, _red);
        _canvas.DrawLine(-1000, -1000, -900, -900, _blue);
        _canvas.DrawLine(50, 200, 50, 150, _blue);
        _canvas.DrawLine(double.NaN, 0, 50, 50, _blue);
        _canvas.DrawLine(-1e9, -50, 1e9, -50, _blue);

        Assert.Equal(10, Find(_red).Count);
        Assert.All(Find(_red), pixel => Assert.Equal(4, pixel.Y));
        Assert.Empty(Find(_blue));
    }

    [Fact]
    public void DrawsPixelLinesWithBresenhamsSteps()
    {
        _canvas.DrawPixelLine(0, 0, 3, 1, _red);
        _canvas.DrawPixelLine(9, 9, 9, 6, _blue);
        _canvas.DrawPixelLine(-5, 5, 1, 5, _blue);

        Assert.Equal([(0, 0), (1, 0), (2, 1), (3, 1)], Find(_red));
        Assert.Equal([(0, 5), (1, 5), (9, 6), (9, 7), (9, 8), (9, 9)], Find(_blue));
    }

    [Fact]
    public void ClipsPixelRectanglesAndPixels()
    {
        _canvas.FillPixels(-5, -5, 2, 2, _red);
        _canvas.SetPixel(10, 0, _blue);
        _canvas.SetPixel(-1, 3, _blue);
        _canvas.SetPixel(9, 9, _blue);

        Assert.Equal([(0, 0), (1, 0), (0, 1), (1, 1)], Find(_red));
        Assert.Equal([(9, 9)], Find(_blue));
    }

    [Fact]
    public void NeedsAnImageOfTheViewsSize()
    {
        var view = new RenderView(new MapRect(0, 0, 100, 100), 10);

        Assert.Throws<ArgumentException>(() => new MapCanvas(new RgbImage(10, 9, default), view));
        Assert.Throws<ArgumentNullException>(() => new MapCanvas(null!, view));
        Assert.Throws<ArgumentNullException>(() => new MapCanvas(new RgbImage(10, 10, default), null!));
    }

    private List<(int X, int Y)> Find(Rgb color)
    {
        var found = new List<(int, int)>();
        for (int y = 0; y < _canvas.Image.Height; y++)
        {
            for (int x = 0; x < _canvas.Image.Width; x++)
            {
                if (_canvas.Image[x, y] == color)
                {
                    found.Add((x, y));
                }
            }
        }

        return found;
    }

    private Rgb[] Row(int y)
    {
        var row = new Rgb[_canvas.Image.Width];
        for (int x = 0; x < row.Length; x++)
        {
            row[x] = _canvas.Image[x, y];
        }

        return row;
    }
}
