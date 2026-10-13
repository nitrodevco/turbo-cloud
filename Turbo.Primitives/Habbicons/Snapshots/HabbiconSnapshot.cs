using Orleans;
using Turbo.Primitives.Habbicons.Enums;

namespace Turbo.Primitives.Habbicons.Snapshots;

/// <summary>One Habbicon of a collection as one player sees it (AS3 <c>habbicon</c>, parser <c>_-Ne</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record HabbiconSnapshot
{
    [Id(0)]
    public required int HabbiconId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required int CollectionId { get; init; }

    [Id(3)]
    public required HabbiconState State { get; init; }

    [Id(4)]
    public required int PriceCredits { get; init; }

    [Id(5)]
    public required int PriceActivityPoints { get; init; }

    [Id(6)]
    public required int ActivityPointType { get; init; }
}
