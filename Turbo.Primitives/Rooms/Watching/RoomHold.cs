using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Watching;

/// <summary>
/// A lease keeping a room loaded and awake: a plugin's own (<see cref="Holder"/>, the plugin's
/// name for it), or the one a player's watch takes (<see cref="Watcher"/>). The same plugin,
/// holder and watcher again renews it.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RoomHold
{
    /// <summary>The plugin's key; empty for the host's own.</summary>
    [Id(0)]
    public required string Plugin { get; init; }

    /// <summary>The plugin's name for the hold; empty for a watch's.</summary>
    [Id(1)]
    public string Holder { get; init; } = string.Empty;

    /// <summary>The player whose watch it is; 0 for a plugin's own hold.</summary>
    [Id(2)]
    public PlayerId Watcher { get; init; }

    [Id(3)]
    public required long LeaseMs { get; init; }
}
