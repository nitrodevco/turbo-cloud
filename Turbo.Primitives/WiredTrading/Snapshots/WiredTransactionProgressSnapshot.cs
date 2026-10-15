using Orleans;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>
/// A player's open contract transaction as the room's <c>@transaction.*</c> user variables read
/// it: when it opened, whose contract it is, whether it was accepted, whether it can be, and how
/// many times over the offer meets the contract.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredTransactionProgressSnapshot
{
    /// <summary>When the transaction opened, in Unix milliseconds.</summary>
    [Id(0)]
    public required long StartedAtMs { get; init; }

    /// <summary>The contract furni, or the Initiate Transaction box for a custom contract.</summary>
    [Id(1)]
    public required RoomObjectId ContractId { get; init; }

    [Id(2)]
    public required WiredTransactionStateType State { get; init; }

    [Id(3)]
    public required bool CanAccept { get; init; }

    /// <summary>How many times over the offer meets the contract now, at most its multiplier.</summary>
    [Id(4)]
    public required int Multiplier { get; init; }
}
