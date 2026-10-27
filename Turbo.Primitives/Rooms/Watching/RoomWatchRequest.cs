using Orleans;
using Turbo.Primitives.Plugins.Messages;

namespace Turbo.Primitives.Rooms.Watching;

/// <summary>A plugin asking for a player to watch a room (<c>IPlayerPresenceGrain.WatchRoomAsync</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record RoomWatchRequest
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    /// <summary>The plugin's key: its unload ends the watch.</summary>
    [Id(1)]
    public required string Plugin { get; init; }

    /// <summary>
    /// The plugin message each of the room's packets is wrapped in (<see cref="PluginEnvelopeComposer"/>):
    /// its fields first, the room's packet after them.
    /// </summary>
    [Id(2)]
    public required PluginMessageComposer Envelope { get; init; }

    [Id(3)]
    public required long LeaseMs { get; init; }

    /// <summary>
    /// The watch is through a linked pair whose other half stands in the room, as core checked
    /// it (never a plugin's say): the room's door lets it through, as it lets a forward through
    /// that pair. Bans still apply.
    /// </summary>
    [Id(4)]
    public bool ThroughLinkedPair { get; init; }
}
