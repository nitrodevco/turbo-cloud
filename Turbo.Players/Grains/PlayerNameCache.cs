using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Turbo.Primitives.Players;

namespace Turbo.Players.Grains;

/// <summary>
/// The player directory's id and name maps, bounded: once it holds <see cref="Capacity"/>
/// players the one used least recently is dropped. Both directions change together, so a name
/// never leads to an id whose name is now something else. Names compare case-insensitively,
/// like the database column.
/// </summary>
internal sealed class PlayerNameCache(int capacity)
{
    private readonly Dictionary<PlayerId, LinkedListNode<(PlayerId Id, string Name)>> _byId = [];
    private readonly Dictionary<string, PlayerId> _idByName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Most recently used first.</summary>
    private readonly LinkedList<(PlayerId Id, string Name)> _recency = new();

    public int Capacity { get; } = Math.Max(1, capacity);

    public bool Contains(PlayerId playerId) => _byId.ContainsKey(playerId);

    public bool TryGetName(PlayerId playerId, [NotNullWhen(true)] out string? name)
    {
        if (!_byId.TryGetValue(playerId, out var node))
        {
            name = null;

            return false;
        }

        Touch(node);

        name = node.Value.Name;

        return true;
    }

    public bool TryGetId(string name, out PlayerId playerId)
    {
        if (!_idByName.TryGetValue(name, out playerId))
            return false;

        Touch(_byId[playerId]);

        return true;
    }

    public void Set(PlayerId playerId, string name)
    {
        Remove(playerId);

        // The name was someone else's until they were renamed; that entry is stale now.
        if (_idByName.TryGetValue(name, out var previousOwner))
            Remove(previousOwner);

        _byId[playerId] = _recency.AddFirst((playerId, name));
        _idByName[name] = playerId;

        while (_byId.Count > Capacity && _recency.Last is { } oldest)
            Remove(oldest.Value.Id);
    }

    private void Remove(PlayerId playerId)
    {
        if (!_byId.Remove(playerId, out var node))
            return;

        _recency.Remove(node);

        if (_idByName.TryGetValue(node.Value.Name, out var id) && id == playerId)
            _idByName.Remove(node.Value.Name);
    }

    private void Touch(LinkedListNode<(PlayerId Id, string Name)> node)
    {
        if (node == _recency.First)
            return;

        _recency.Remove(node);
        _recency.AddFirst(node);
    }
}
