using Orleans;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Wired;

/// <summary>
/// A player completed a transaction wired offered them: paid, traded or was rewarded.
/// <see cref="SourceId"/> is the contract furni, or the Initiate Transaction box when a custom
/// contract was used; the transaction triggers select by it.
/// </summary>
[GenerateSerializer]
public sealed record WiredTransactionCompletedEvent : PlayerEvent
{
    [Id(0)]
    public required RoomObjectId SourceId { get; init; }
}
