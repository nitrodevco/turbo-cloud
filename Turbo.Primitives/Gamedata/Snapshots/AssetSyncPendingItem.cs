using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>One of Habbo's libraries a sync would download, and why.</summary>
[GenerateSerializer, Immutable]
public sealed record AssetSyncPendingItem
{
    [Id(0)]
    public required string Name { get; init; }

    [Id(1)]
    public required AssetSyncReason Reason { get; init; }

    /// <summary>The revision Habbo serves it at.</summary>
    [Id(2)]
    public required string HabboRevision { get; init; }

    /// <summary>The revision the hotel has; null when it has no row of it.</summary>
    [Id(3)]
    public string? HotelRevision { get; init; }

    /// <summary>Why the hotel's last try failed, when it did.</summary>
    [Id(4)]
    public string? Error { get; init; }
}
