using Orleans;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.Rooms.Events.Wired;

/// <summary>
/// A transaction wired offered a player did not go through: they cancelled, it timed out, wired
/// cancelled it, or the chests could not carry it out. <see cref="SourceId"/> is as on
/// <see cref="WiredTransactionCompletedEvent"/>.
/// </summary>
[GenerateSerializer]
public sealed record WiredTransactionFailedEvent : PlayerEvent
{
    [Id(0)]
    public required RoomObjectId SourceId { get; init; }

    [Id(1)]
    public required WiredTransactionFailureType Reason { get; init; }
}
