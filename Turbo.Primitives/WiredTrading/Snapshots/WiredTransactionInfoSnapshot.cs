using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>One wired transaction as a log row shows it.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredTransactionInfoSnapshot
{
    [Id(0)]
    public required long TransactionId { get; init; }

    [Id(1)]
    public required RoomId FlatId { get; init; }

    [Id(2)]
    public required WiredTransactionType Type { get; init; }

    [Id(3)]
    public required string DefinitionInfo { get; init; }

    [Id(4)]
    public required PlayerId UserId { get; init; }

    [Id(5)]
    public required string UserName { get; init; }

    [Id(6)]
    public required long Timestamp { get; init; }

    [Id(7)]
    public required string ReadableTimestamp { get; init; }

    [Id(8)]
    public required int ChestCount { get; init; }

    [Id(9)]
    public required int WithdrawFurniCount { get; init; }

    [Id(10)]
    public required int DepositFurniCount { get; init; }

    [Id(11)]
    public required int WithdrawCoinsCount { get; init; }

    [Id(12)]
    public required int DepositCoinsCount { get; init; }
}
