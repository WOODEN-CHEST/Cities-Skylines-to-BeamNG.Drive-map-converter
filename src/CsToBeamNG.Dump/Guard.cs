using System;
using System.Diagnostics.CodeAnalysis;

namespace CsToBeamNG.Dump;

/// <summary>
/// Argument and state checks for code that compiles for both .NET 3.5 and .NET 10. The .NET 10 build
/// uses the framework's throw helpers, which the analyzers ask for there; .NET 3.5 lacks them.
/// </summary>
internal static class Guard
{
    public static void NotNull([NotNull] object? value, string parameterName)
    {
#if NET
        ArgumentNullException.ThrowIfNull(value, parameterName);
#else
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }
#endif
    }

    public static void NotDisposed([DoesNotReturnIf(true)] bool disposed, object instance)
    {
#if NET
        ObjectDisposedException.ThrowIf(disposed, instance);
#else
        if (disposed)
        {
            throw new ObjectDisposedException(instance.GetType().FullName);
        }
#endif
    }
}
