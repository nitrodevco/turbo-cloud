using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>A page of a chest's or a room's wired transaction log.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredTransactionLogListSnapshot
{
    [Id(0)]
    public required WiredTransactionLogListType ListType { get; init; }

    /// <summary>The chest id or the room id, by <see cref="ListType"/>.</summary>
    [Id(1)]
    public required long ListId { get; init; }

    [Id(2)]
    public required int TotalLogs { get; init; }

    [Id(3)]
    public required int CurrentPage { get; init; }

    [Id(4)]
    public required int PageSize { get; init; }

    [Id(5)]
    public required ImmutableArray<WiredTransactionInfoSnapshot> Logs { get; init; }
}
