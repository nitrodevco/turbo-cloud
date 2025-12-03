using Orleans;

namespace Turbo.Primitives.Inventory.Snapshots;

[GenerateSerializer, Immutable]
public sealed record AvatarEffectSnapshot
{
    [Id(0)]
    public required int Type { get; init; }

    [Id(1)]
    public required int SubType { get; init; }

    [Id(2)]
    public required int Duration { get; init; }

    [Id(3)]
    public required int InactiveEffectsInInventory { get; init; }

    [Id(4)]
    public required int SecondsLeftIfActive { get; init; }

    [Id(5)]
    public required bool IsPermanent { get; init; }
}
