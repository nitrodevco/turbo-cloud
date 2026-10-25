using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Wired;

/// <summary>
/// A plugin's wired event (<c>Turbo.Contracts.Wired.IWiredEvent</c>) fired in a room: the plugin
/// triggers whose event has <see cref="EventKey"/> hear it. The event itself is carried as JSON,
/// because its type is the plugin's and has no serializer of the host's. A player who triggers it
/// is <see cref="RoomEvent.CausedBy"/>; a pet or a bot is <see cref="TriggererId"/>.
/// </summary>
[GenerateSerializer]
public sealed record PluginWiredEvent : RoomEvent
{
    /// <summary><c>WiredEvents.KeyOf</c> the event's type.</summary>
    [Id(0)]
    public required string EventKey { get; init; }

    [Id(1)]
    public required string Payload { get; init; }

    [Id(2)]
    public RoomObjectId? TriggererId { get; init; }
}
