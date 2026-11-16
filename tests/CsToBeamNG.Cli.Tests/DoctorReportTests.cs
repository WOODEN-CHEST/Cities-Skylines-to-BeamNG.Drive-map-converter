using System;
using System.IO;
using CsToBeamNG.Converter.Discovery;
using Xunit;

namespace CsToBeamNG.Cli.Tests;

public sealed class DoctorReportTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(999, "999 B")]
    [InlineData(1000, "1.0 kB")]
    [InlineData(8_649_140, "8.6 MB")]
    [InlineData(56_074_500_701, "56.1 GB")]
    public void FormatsSizesWithDecimalUnits(long bytes, string expected)
    {
        Assert.Equal(expected, DoctorReport.FormatSize(bytes));
    }

    [Fact]
    public void WritesNumbersAndDatesWithTheInvariantCulture()
    {
        // Latvian uses a decimal comma, like the maintainer's machine.
        using var culture = new CultureScope("lv-LV");
        var save = new SaveGame("Seaside City", "/saves/Seaside City.crp", 8_649_140, new DateTime(2018, 11, 10, 12, 18, 0, DateTimeKind.Utc), SaveLocation.SteamCloud, "12345678");
        GameEnvironment environment = CreateEnvironment() with
        {
            CitiesSkylines = new CitiesSkylinesInfo { Saves = [save], WorkshopItemCount = 1234 },
        };

        string report = Write(environment);

        Assert.Contains("Seaside City", report, StringComparison.Ordinal);
        Assert.Contains("8.6 MB", report, StringComparison.Ordinal);
        Assert.Contains("2018-11-10 12:18", report, StringComparison.Ordinal);
        Assert.Contains("Steam Cloud, account 12345678", report, StringComparison.Ordinal);
        Assert.DoesNotContain("8,6", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowsWhereLocationsCameFromAndTheResult()
    {
        GameEnvironment environment = CreateEnvironment() with
        {
            BeamNG = new BeamNGInfo
            {
                InstallDir = new FoundPath("/games/BeamNG.drive", LocationSource.Override, Exists: true, "CSTOBEAMNG_BEAMNG_DIR"),
                UserDir = new FoundPath("/home/user/.local/share/BeamNG/BeamNG.drive/current", LocationSource.Default, Exists: false),
                Version = "0.40.5.0",
                EngineBuild = "302427",
                VersionSource = "integrity.json",
            },
            Problems = [new DiscoveryProblem(ProblemSeverity.Error, "BeamNG.drive user folder: not found.")],
        };

        string report = Write(environment);

        Assert.Contains("/games/BeamNG.drive (from CSTOBEAMNG_BEAMNG_DIR)", report, StringComparison.Ordinal);
        Assert.Contains("/home/user/.local/share/BeamNG/BeamNG.drive/current (default, missing)", report, StringComparison.Ordinal);
        Assert.Contains("0.40.5.0, build 302427 (from integrity.json)", report, StringComparison.Ordinal);
        Assert.Contains("error:   BeamNG.drive user folder: not found.", report, StringComparison.Ordinal);
        Assert.Contains("Result: something essential is missing", report, StringComparison.Ordinal);
    }

    private static GameEnvironment CreateEnvironment() => new()
    {
        Os = OsKind.Linux,
        Steam = new SteamInfo([], []),
        CitiesSkylines = new CitiesSkylinesInfo(),
        BeamNG = new BeamNGInfo(),
        Problems = [],
    };

    private static string Write(GameEnvironment environment)
    {
        using var output = new StringWriter();
        DoctorReport.Write(environment, output, TimeZoneInfo.Utc);
        return output.ToString();
    }
}
