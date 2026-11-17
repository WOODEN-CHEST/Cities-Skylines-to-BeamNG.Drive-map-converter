namespace CsToBeamNG.Converter.BeamNG.Level;

/// <summary>A spawn point listed in <c>info.json</c>.</summary>
/// <param name="ObjectName">The name of the <c>SpawnSphere</c> in the scene tree.</param>
/// <param name="Title">The name shown to the player (the <c>translationId</c> field; plain text is shown as is).</param>
public sealed record LevelSpawnPoint(string ObjectName, string Title);
