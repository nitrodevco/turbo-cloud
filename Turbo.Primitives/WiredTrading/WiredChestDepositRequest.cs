using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.WiredTrading;

/// <summary>
/// Items a player puts into a chest from their inventory. Into a credit chest only credit furni
/// go, and they become credits; into a furni chest any tradeable furni but credit furni.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredChestDepositRequest
{
    [Id(0)]
    public required WiredChestSettingsSnapshot Chest { get; init; }

    [Id(1)]
    public required PlayerId DepositorId { get; init; }

    [Id(2)]
    public required string DepositorName { get; init; }

    [Id(3)]
    public required ImmutableArray<RoomObjectId> ItemIds { get; init; }

    /// <summary>The most the chest may hold after the deposit, as its owner set it.</summary>
    [Id(4)]
    public required int Capacity { get; init; }

    [Id(5)]
    public required WiredTransactionType Type { get; init; }

    [Id(6)]
    public required string DefinitionInfo { get; init; }
}
