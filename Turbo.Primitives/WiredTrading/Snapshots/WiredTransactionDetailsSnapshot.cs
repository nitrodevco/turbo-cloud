using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>One wired transaction in full: the chests it touched and the furni it moved each way.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredTransactionDetailsSnapshot
{
    [Id(0)]
    public required WiredTransactionInfoSnapshot Info { get; init; }

    [Id(1)]
    public required ImmutableArray<int> ChestIds { get; init; }

    [Id(2)]
    public required ImmutableArray<WiredTransactionItemCountSnapshot> Deposited { get; init; }

    [Id(3)]
    public required ImmutableArray<WiredTransactionItemCountSnapshot> Withdrawn { get; init; }

    [Id(4)]
    public required bool IsIncompleteData { get; init; }
}
