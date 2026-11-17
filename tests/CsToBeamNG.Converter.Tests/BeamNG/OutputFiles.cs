using System.Collections.Generic;
using System.Linq;
using System.Text;
using CsToBeamNG.Converter.BeamNG;

namespace CsToBeamNG.Converter.Tests.BeamNG;

/// <summary>Helpers for inspecting generated files in tests.</summary>
internal static class OutputFiles
{
    public static string Text(this OutputFile file) => Encoding.UTF8.GetString(file.ToBytes());

    public static OutputFile Single(this IEnumerable<OutputFile> files, string path) =>
        files.Single(file => file.Path.Value == path);

    /// <summary>The content of every file, keyed by path, for comparing whole outputs.</summary>
    public static Dictionary<string, byte[]> Snapshot(IEnumerable<OutputFile> files) =>
        files.ToDictionary(file => file.Path.Value, file => file.ToBytes());
}
