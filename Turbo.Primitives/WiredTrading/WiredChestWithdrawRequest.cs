using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.WiredTrading;

/// <summary>
/// Credits or furni a chest hands to a player: the owner withdrawing, or wired giving. Null
/// <see cref="Amount"/> takes everything (of <see cref="ItemType"/>, when one is named).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredChestWithdrawRequest
{
    [Id(0)]
    public required WiredChestSettingsSnapshot Chest { get; init; }

    [Id(1)]
    public required PlayerId ReceiverId { get; init; }

    [Id(2)]
    public required string ReceiverName { get; init; }

    [Id(3)]
    public required int? Amount { get; init; }

    /// <summary>Only furni of this type; null for any.</summary>
    [Id(4)]
    public required ChestItemTypeSnapshot? ItemType { get; init; }

    [Id(5)]
    public required WiredChestIterationMode Order { get; init; }

    [Id(6)]
    public required WiredTransactionType Type { get; init; }

    [Id(7)]
    public required string DefinitionInfo { get; init; }
}
