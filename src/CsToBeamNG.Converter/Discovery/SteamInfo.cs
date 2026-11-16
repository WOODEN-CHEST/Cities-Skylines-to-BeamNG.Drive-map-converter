using System.Collections.Generic;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>The Steam installations found.</summary>
/// <param name="Roots">
/// The Steam folders (each containing <c>steamapps</c>), with symbolic links resolved and duplicates
/// removed.
/// </param>
/// <param name="Libraries">Every Steam library folder, the roots included, in search order.</param>
public sealed record SteamInfo(IReadOnlyList<FoundPath> Roots, IReadOnlyList<string> Libraries);
