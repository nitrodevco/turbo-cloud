using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>
/// How a deposit or withdrawal went. On failure nothing moved and <see cref="Failure"/> says
/// why; the summary is the chest as it stands either way.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredChestMoveResultSnapshot
{
    [Id(0)]
    public required WiredTransactionFailureType? Failure { get; init; }

    /// <summary>The logged transaction; 0 when nothing moved.</summary>
    [Id(1)]
    public required long TransactionId { get; init; }

    [Id(2)]
    public required int Coins { get; init; }

    /// <summary>The furni that moved, counted by type.</summary>
    [Id(3)]
    public required ImmutableArray<WiredTransactionItemCountSnapshot> Items { get; init; }

    [Id(4)]
    public required WiredChestSummarySnapshot Summary { get; init; }

    public bool Succeeded => Failure is null;

    public static WiredChestMoveResultSnapshot Failed(
        WiredTransactionFailureType failure,
        WiredChestSummarySnapshot summary
    ) =>
        new()
        {
            Failure = failure,
            TransactionId = 0,
            Coins = 0,
            Items = [],
            Summary = summary,
        };
}
