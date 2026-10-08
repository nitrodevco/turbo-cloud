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

    /// <summary>How many times over the contract was taken.</summary>
    [Id(1)]
    public int Multiplier { get; init; }

    /// <summary>Furni the player paid into the chests.</summary>
    [Id(2)]
    public int DepositFurniCount { get; init; }

    /// <summary>Credits the player paid into the chests.</summary>
    [Id(3)]
    public int DepositCoinsCount { get; init; }

    /// <summary>Furni the chests gave the player.</summary>
    [Id(4)]
    public int WithdrawalFurniCount { get; init; }

    /// <summary>Credits the chests gave the player.</summary>
    [Id(5)]
    public int WithdrawalCoinsCount { get; init; }
}
