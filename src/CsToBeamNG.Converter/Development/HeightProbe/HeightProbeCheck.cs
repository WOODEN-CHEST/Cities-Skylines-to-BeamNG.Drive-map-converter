using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CsToBeamNG.Converter.Terrain;

namespace CsToBeamNG.Converter.Development.HeightProbe;

/// <summary>
/// Compares the converter's reading of a dump's terrain with the game's own height samplers, as recorded by a heights
/// probe (<see cref="HeightProbeFile"/>), and summarises how high the probed objects stand above the ground. This is
/// how the height-source findings of docs/research/cs1-save-and-world.md, Terrain were checked.
/// </summary>
public static class HeightProbeCheck
{
    /// <summary>
    /// The largest difference from the game's sampler that still counts as a match, in metres. The game computes grid
    /// coordinates in 32-bit floats, whose last digit near the map's edge is about 2 mm; on the steepest slopes, about
    /// 2 m per metre, that moves its heights by up to about 4 mm. The converter computes in 64-bit floats.
    /// </summary>
    public const double Tolerance = 0.005;

    /// <summary>The probe's name for the comparison of the raw heights with the copy the game's raw samplers read.</summary>
    public const string RawCopyComparison = "rawVsRaw2";

    /// <summary>
    /// Compares the converter's samplers with the game's at every probed position: <see cref="CityTerrain.SampleGround"/>
    /// with <c>SampleDetailHeight</c>, and the final and raw heights with <c>SampleFinalHeight</c> and
    /// <c>SampleRawHeight</c>. The raw comparison is left out when the probe found that the game's copy of the raw
    /// heights, which <c>SampleRawHeight</c> reads, differed from the raw heights the dump holds.
    /// </summary>
    /// <param name="terrain">The dump's terrain.</param>
    /// <param name="probe">The probe from the same export.</param>
    public static IReadOnlyList<HeightProbeComparison> Compare(CityTerrain terrain, HeightProbeFile probe)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(probe);
        var comparisons = new List<HeightProbeComparison>
        {
            CompareSampler("ground: SampleGround vs SampleDetailHeight", probe.Samples, s => terrain.SampleGround(s.X, s.Z), s => s.Detail),
            CompareSampler("final heights vs SampleFinalHeight", probe.Samples, s => terrain.FinalHeights.Sample(s.X, s.Z), s => s.Final),
        };
        if (probe.Arrays.FirstOrDefault(a => a.Name == RawCopyComparison) is { Differing: 0 })
        {
            comparisons.Add(CompareSampler("raw heights vs SampleRawHeight", probe.Samples, s => terrain.RawHeights.Sample(s.X, s.Z), s => s.Raw));
        }

        return comparisons;
    }

    /// <summary>
    /// Summarises, for each kind of probed object, how far its own height lies above the game's samplers: the ground
    /// (<c>SampleDetailHeight</c>), the final heights and the raw heights roads and buildings are placed by. Medians are
    /// used because the kinds mix objects that sit on the ground with buried pipes and bridges.
    /// </summary>
    /// <param name="probe">The probe.</param>
    public static IReadOnlyList<ObjectHeightSummary> SummarizeObjects(HeightProbeFile probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        var summaries = new List<ObjectHeightSummary>();
        foreach (IGrouping<string, HeightProbeSample> group in probe.Samples.Where(s => s.Kind != "point").GroupBy(s => s.Kind))
        {
            double[] aboveGround = [.. group.Select(s => s.Y - s.Detail)];
            summaries.Add(new ObjectHeightSummary(
                group.Key,
                aboveGround.Length,
                Median(aboveGround),
                aboveGround.Count(d => Math.Abs(d) <= 0.02),
                Median([.. group.Select(s => s.Y - s.Final)]),
                Median([.. group.Select(s => s.Y - s.RawSmooth)])));
        }

        return summaries;
    }

    /// <summary>Formats the comparisons and the object summaries as a table for the console.</summary>
    /// <param name="comparisons">The comparisons from <see cref="Compare"/>.</param>
    /// <param name="objects">The summaries from <see cref="SummarizeObjects"/>.</param>
    public static string FormatReport(IReadOnlyList<HeightProbeComparison> comparisons, IReadOnlyList<ObjectHeightSummary> objects)
    {
        ArgumentNullException.ThrowIfNull(comparisons);
        ArgumentNullException.ThrowIfNull(objects);
        var text = new StringBuilder();
        text.AppendLine("The converter's heights vs the game's samplers at the probed positions:");
        foreach (HeightProbeComparison comparison in comparisons)
        {
            text.Append(CultureInfo.InvariantCulture, $"  {comparison.Name,-45} {comparison.Count,4} positions, max {comparison.MaxErrorMeters * 1000,8:0.000} mm, mean {comparison.MeanErrorMeters * 1000,8:0.000} mm: {(comparison.IsMatch ? "ok" : "DIFFERENT")}");
            if (!comparison.IsMatch && comparison.Worst is { } worst)
            {
                text.Append(CultureInfo.InvariantCulture, $" (worst: {worst.Kind} {worst.Id} at x {worst.X:0.###}, z {worst.Z:0.###})");
            }

            text.AppendLine();
        }

        text.AppendLine();
        text.AppendLine("The probed objects' own heights above the game's samplers (medians, metres):");
        foreach (ObjectHeightSummary summary in objects)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {summary.Kind,-12} {summary.Count,4}: above the ground {summary.MedianAboveGround,7:+0.000;-0.000} ({summary.WithinTwoCentimetres} within 2 cm), above the final heights {summary.MedianAboveFinal,7:+0.000;-0.000}, above the raw heights (smooth) {summary.MedianAboveRawSmooth,7:+0.000;-0.000}"));
        }

        return text.ToString();
    }

    private static HeightProbeComparison CompareSampler(
        string name,
        IReadOnlyList<HeightProbeSample> samples,
        Func<HeightProbeSample, double> converter,
        Func<HeightProbeSample, double> game)
    {
        double max = 0;
        double sum = 0;
        HeightProbeSample? worst = null;
        foreach (HeightProbeSample sample in samples)
        {
            double error = Math.Abs(converter(sample) - game(sample));
            sum += error;
            if (worst is null || error > max)
            {
                max = error;
                worst = sample;
            }
        }

        return new HeightProbeComparison(name, samples.Count, max, samples.Count == 0 ? 0 : sum / samples.Count, worst);
    }

    private static double Median(double[] values)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        Array.Sort(values);
        int middle = values.Length / 2;
        return values.Length % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2;
    }
}

/// <summary>How closely the converter reproduced one of the game's height samplers.</summary>
/// <param name="Name">Which sampler.</param>
/// <param name="Count">The number of positions compared.</param>
/// <param name="MaxErrorMeters">The largest difference, in metres.</param>
/// <param name="MeanErrorMeters">The mean difference, in metres.</param>
/// <param name="Worst">The position with the largest difference, or <see langword="null"/> without positions.</param>
public sealed record HeightProbeComparison(string Name, int Count, double MaxErrorMeters, double MeanErrorMeters, HeightProbeSample? Worst)
{
    /// <summary>Whether every difference is within <see cref="HeightProbeCheck.Tolerance"/>.</summary>
    public bool IsMatch => MaxErrorMeters <= HeightProbeCheck.Tolerance;
}

/// <summary>How high the probed objects of one kind stand above the game's height samplers.</summary>
/// <param name="Kind">The kind of object, such as <c>node</c>.</param>
/// <param name="Count">The number of objects.</param>
/// <param name="MedianAboveGround">The median of the object's height minus the ground (<c>SampleDetailHeight</c>), in metres.</param>
/// <param name="WithinTwoCentimetres">How many objects are within 2 cm of the ground.</param>
/// <param name="MedianAboveFinal">The median of the object's height minus the final heights, in metres.</param>
/// <param name="MedianAboveRawSmooth">The median of the object's height minus <c>SampleRawHeightSmooth</c>, in metres.</param>
public sealed record ObjectHeightSummary(
    string Kind,
    int Count,
    double MedianAboveGround,
    int WithinTwoCentimetres,
    double MedianAboveFinal,
    double MedianAboveRawSmooth);
