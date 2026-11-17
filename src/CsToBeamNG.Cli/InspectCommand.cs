using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using CsToBeamNG.Converter.Imaging;
using CsToBeamNG.Converter.Loading;
using CsToBeamNG.Converter.Planning;
using CsToBeamNG.Converter.Rendering;
using CsToBeamNG.Dump;
using CsToBeamNG.Dump.Manifest;

namespace CsToBeamNG.Cli;

/// <summary><c>cstobeamng inspect</c>: summarises a city dump and optionally draws it from above.</summary>
internal static class InspectCommand
{
    /// <summary>The exit code when the dump, a section of it or the image couldn't be read or written.</summary>
    public const int FailedExitCode = 1;

    /// <summary>The default length of the rendered image's longer side, in pixels.</summary>
    public const int DefaultRenderSize = 1024;

    /// <summary>The smallest image size <c>--size</c> accepts.</summary>
    public const int MinRenderSize = 16;

    public static Command Create()
    {
        var dump = new Argument<string>("dump")
        {
            Description = "The dump folder, or its manifest.json. `cstobeamng doctor` shows where the exporter puts dumps.",
        };
        var tiles = new Option<IReadOnlyList<TileCoordinate>?>("--tiles")
        {
            Description = "Overrides the area of interest with a block of map tiles: " + MapAreaSyntax.TileRule + ".",
            HelpName = "x,z",
            CustomParser = ParseTiles,
        };
        var render = new Option<string?>("--render")
        {
            Description = "Also draws the city from above into this PNG file: +X (east) right, +Z (north; BeamNG's +Y) up.",
            HelpName = "file.png",
        };
        render.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<string?>() is { } path && !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                result.AddError("--render takes the name of a .png file.");
            }
        });
        var size = new Option<int>("--size")
        {
            Description = $"With --render: the image's longer side, in pixels, from {MinRenderSize} to {RenderView.MaxSizePixels}.",
            HelpName = "pixels",
            DefaultValueFactory = _ => DefaultRenderSize,
        };
        size.Validators.Add(result =>
        {
            int value = result.GetValueOrDefault<int>();
            if (value < MinRenderSize || value > RenderView.MaxSizePixels)
            {
                result.AddError($"--size must be from {MinRenderSize} to {RenderView.MaxSizePixels} pixels.");
            }
        });
        var area = new Option<RenderArea>("--area")
        {
            Description = "With --render: the part of the map to draw: aoi (the area of interest), map (the whole map), "
                + "a block of tiles such as 3-5,4, or minX,minZ,maxX,maxZ in metres in the game's coordinates, such as -500,-500,500,500.",
            HelpName = "area",
            DefaultValueFactory = _ => RenderArea.AreaOfInterest,
            CustomParser = ParseArea,
        };
        var command = new Command(
            "inspect",
            "Summarises a city dump: the manifest, each section with its statistics, the area of interest the conversion "
            + "would cover, and the warnings. Exits with 1 if the dump or one of its sections can't be read.");
        command.Arguments.Add(dump);
        command.Options.Add(tiles);
        command.Options.Add(render);
        command.Options.Add(size);
        command.Options.Add(area);
        command.Validators.Add(result =>
        {
            bool sizeGiven = result.GetResult(size) is { Implicit: false };
            bool areaGiven = result.GetResult(area) is { Implicit: false };
            if ((sizeGiven || areaGiven) && result.GetValue(render) is null)
            {
                result.AddError("--size and --area only apply to an image; add --render <file.png>.");
            }
        });
        command.SetAction(parseResult => Inspect(
            new InspectRequest(parseResult.GetRequiredValue(dump))
            {
                Tiles = parseResult.GetValue(tiles),
                RenderPath = parseResult.GetValue(render),
                RenderSize = parseResult.GetValue(size),
                RenderArea = parseResult.GetValue(area) ?? RenderArea.AreaOfInterest,
            },
            parseResult.InvocationConfiguration.Output,
            parseResult.InvocationConfiguration.Error));
        return command;
    }

    internal static int Inspect(InspectRequest request, TextWriter output, TextWriter error)
    {
        string folder = request.DumpPath;
        if (File.Exists(folder) && string.Equals(Path.GetFileName(folder), DumpFormat.ManifestFileName, StringComparison.Ordinal))
        {
            folder = Path.GetDirectoryName(Path.GetFullPath(folder))!;
        }

        WorldLoadResult loaded;
        try
        {
            // Diagnostics look at incomplete dumps too; the report warns about them.
            loaded = WorldLoader.Load(folder, new WorldLoadOptions { AllowIncomplete = true });
        }
        catch (DumpException e)
        {
            error.WriteLine("error: " + e.Message);
            return FailedExitCode;
        }

        AreaOfInterest areaOfInterest = AreaOfInterest.Plan(loaded.World, new AreaOfInterestOptions { Tiles = request.Tiles });
        InspectReport.Write(loaded, areaOfInterest, output);
        int exitCode = loaded.HasErrors ? FailedExitCode : 0;
        if (request.RenderPath is not null && !Render(loaded, areaOfInterest, request, output, error))
        {
            exitCode = FailedExitCode;
        }

        return exitCode;
    }

    private static bool Render(WorldLoadResult loaded, AreaOfInterest areaOfInterest, InspectRequest request, TextWriter output, TextWriter error)
    {
        var view = new RenderView(request.RenderArea.Resolve(areaOfInterest), request.RenderSize);
        IReadOnlyList<IRenderLayer> layers = RenderLayers.CreateDefault();
        RgbImage image = TopDownRenderer.Render(loaded.World, areaOfInterest, view, layers);
        string path = Path.GetFullPath(request.RenderPath!);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using FileStream file = File.Create(path);
            PngEncoder.Write(file, image);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"error: the image couldn't be written to '{path}': {e.Message}");
            return false;
        }

        output.WriteLine();
        InspectReport.WriteRender(path, request.RenderArea, view, layers, output);
        return true;
    }

    private static IReadOnlyList<TileCoordinate>? ParseTiles(ArgumentResult result)
    {
        if (MapAreaSyntax.TryParseTiles(result.Tokens[0].Value, out IReadOnlyList<TileCoordinate>? tiles, out string? message))
        {
            return tiles;
        }

        result.AddError(message);
        return null;
    }

    private static RenderArea? ParseArea(ArgumentResult result)
    {
        if (MapAreaSyntax.TryParseArea(result.Tokens[0].Value, out RenderArea? area, out string? message))
        {
            return area;
        }

        result.AddError(message);
        return null;
    }
}
