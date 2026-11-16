namespace CsToBeamNG.Converter.Discovery;

/// <summary>Reads string values from the Windows registry. Tests supply fakes.</summary>
public interface IRegistryReader
{
    /// <summary>Reads a string value.</summary>
    /// <param name="root">The hive.</param>
    /// <param name="keyPath">The key below the hive, for example <c>Software\Valve\Steam</c>.</param>
    /// <param name="valueName">The name of the value.</param>
    /// <returns>The value, or <see langword="null"/> if the key or value doesn't exist or isn't a string.</returns>
    string? GetString(RegistryRoot root, string keyPath, string valueName);
}
