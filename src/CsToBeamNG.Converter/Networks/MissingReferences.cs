using System.Globalization;
using CsToBeamNG.Converter.Loading;

namespace CsToBeamNG.Converter.Networks;

/// <summary>
/// Counts objects that refer to something the dump lacks, such as segments whose node is missing, and reports them in
/// one warning with an example, so a damaged dump doesn't produce thousands of messages. The objects are kept: their
/// own data is still usable.
/// </summary>
internal sealed class MissingReferences
{
    private readonly string _problem;
    private int _count;
    private string? _example;

    /// <param name="problem">What the objects do, in plural words, such as <c>segments end at nodes the dump doesn't have</c>.</param>
    public MissingReferences(string problem) => _problem = problem;

    /// <summary>Counts an object.</summary>
    /// <param name="example">The object and what it refers to, such as <c>segment 12 (node 34)</c>.</param>
    public void Add(string example)
    {
        _count++;
        _example ??= example;
    }

    /// <summary>Reports the objects as one warning, if there are any.</summary>
    public void Report(SectionLoadContext context)
    {
        if (_count > 0)
        {
            context.Warn(string.Create(CultureInfo.InvariantCulture, $"{_count} {_problem}, such as {_example}; they are kept."));
        }
    }
}
