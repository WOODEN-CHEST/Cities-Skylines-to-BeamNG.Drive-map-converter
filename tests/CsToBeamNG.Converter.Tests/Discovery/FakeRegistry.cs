using System.Collections.Generic;
using CsToBeamNG.Converter.Discovery;

namespace CsToBeamNG.Converter.Tests.Discovery;

internal sealed class FakeRegistry : IRegistryReader
{
    private readonly Dictionary<(RegistryRoot, string, string), string> _values = [];

    public FakeRegistry Set(RegistryRoot root, string keyPath, string valueName, string value)
    {
        _values[(root, keyPath, valueName)] = value;
        return this;
    }

    public string? GetString(RegistryRoot root, string keyPath, string valueName) =>
        _values.TryGetValue((root, keyPath, valueName), out string? value) ? value : null;
}
