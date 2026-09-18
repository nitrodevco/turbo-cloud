using Orleans;

namespace Turbo.Primitives.Players.Snapshots;

[GenerateSerializer, Immutable]
public sealed record PlayerBadgeSnapshot
{
    [Id(0)]
    public required int SlotId { get; init; }

    [Id(1)]
    public required string BadgeCode { get; init; }
}
