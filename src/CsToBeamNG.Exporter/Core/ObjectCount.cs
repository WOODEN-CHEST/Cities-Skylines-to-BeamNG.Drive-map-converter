namespace CsToBeamNG.Exporter.Core;

/// <summary>How many objects of one kind the city has, counted in the game's instance buffer.</summary>
internal sealed class ObjectCount
{
    /// <summary>Creates a count.</summary>
    /// <param name="key">The key in the manifest's <c>counts</c>, from <see cref="CountKeys"/>.</param>
    /// <param name="created">The number of buffer entries whose <c>Created</c> flag is set.</param>
    /// <param name="gameCounter">The manager's own counter, such as <c>NetManager.m_nodeCount</c>, for comparison.</param>
    /// <param name="capacity">The buffer's length, which mods that raise the limits change.</param>
    public ObjectCount(string key, long created, long gameCounter, long capacity)
    {
        Key = key;
        Created = created;
        GameCounter = gameCounter;
        Capacity = capacity;
    }

    /// <summary>The key in the manifest's <c>counts</c>.</summary>
    public string Key { get; }

    /// <summary>The number of objects: buffer entries whose <c>Created</c> flag is set.</summary>
    public long Created { get; }

    /// <summary>The manager's own counter. It should equal <see cref="Created"/>; the log notes when it doesn't.</summary>
    public long GameCounter { get; }

    /// <summary>The buffer's length: the most objects of this kind the game can hold.</summary>
    public long Capacity { get; }
}
