using Orleans;
using Turbo.Primitives.Furniture.Snapshots.StuffData;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>One item stored in a wired chest, as the chest's contents list shows it.</summary>
[GenerateSerializer, Immutable]
public sealed record ChestStorageSnapshot
{
    /// <summary>The item's inventory id.</summary>
    [Id(0)]
    public required int ItemId { get; init; }

    [Id(1)]
    public required int LockState { get; init; }

    /// <summary>The transaction that put the item in the chest.</summary>
    [Id(2)]
    public required long TransactionId { get; init; }

    [Id(3)]
    public required ChestItemTypeSnapshot Type { get; init; }

    [Id(4)]
    public required bool Groupable { get; init; }

    [Id(5)]
    public required int SpecialType { get; init; }

    [Id(6)]
    public required StuffDataSnapshot StuffData { get; init; }

    /// <summary>Written for a floor item only; the client does not read it for a wall item.</summary>
    [Id(7)]
    public required int Extra { get; init; }
}
