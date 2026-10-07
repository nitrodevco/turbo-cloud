using Orleans;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>What a wired chest's owner wants to be told about it.</summary>
[GenerateSerializer, Immutable]
public sealed record SetChestNotificationPreferencesInteraction : FurnitureInteraction
{
    [Id(0)]
    public required WiredChestNotifyMode NotifyMode { get; init; }

    [Id(1)]
    public required bool OnChestFull { get; init; }

    [Id(2)]
    public required bool OnDonation { get; init; }

    [Id(3)]
    public required bool OnWithdraw { get; init; }

    [Id(4)]
    public required bool OnChestEmpty { get; init; }

    [Id(5)]
    public required bool OnWiredTransaction { get; init; }
}
