using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.WiredTrading;

/// <summary>
/// A payment or trade wired offers a player (Initiate Transaction): what the contract asks and
/// gives, how many times, the chests it pays into and out of, and how long the player has.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredContractTradeRequest
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    /// <summary>The contract furni, or the Initiate Transaction box for a custom contract.</summary>
    [Id(1)]
    public required RoomObjectId SourceId { get; init; }

    [Id(2)]
    public required WiredContractSnapshot Contract { get; init; }

    [Id(3)]
    public required TradeRequirementRulesType RulesType { get; init; }

    /// <summary>
    /// How many times the requirements must be met (multiplier), or may be met at most
    /// (auto-multiplier). One otherwise.
    /// </summary>
    [Id(4)]
    public required int Multiplier { get; init; }

    [Id(5)]
    public required ImmutableArray<RoomObjectId> ChestIds { get; init; }

    /// <summary>Zero for no time limit.</summary>
    [Id(6)]
    public required int TimeoutSeconds { get; init; }

    /// <summary>A payment that takes whatever the player offers.</summary>
    public bool IsDonation =>
        Contract.Type == WiredContractType.Payment
        && Contract.PaymentMode == WiredContractPaymentMode.Donation;
}
