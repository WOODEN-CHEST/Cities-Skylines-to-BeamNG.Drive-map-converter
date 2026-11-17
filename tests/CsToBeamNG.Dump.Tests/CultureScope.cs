using System;
using System.Globalization;

namespace CsToBeamNG.Dump.Tests;

/// <summary>Sets the current culture until disposed, for tests that must run under a decimal-comma culture.</summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _previousUICulture = CultureInfo.CurrentUICulture;

    public CultureScope(string name)
    {
        var culture = new CultureInfo(name);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUICulture;
    }
}
