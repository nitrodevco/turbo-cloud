using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// One kind's libraries as a sync would find them: how many Habbo lists, how many the hotel has
/// at Habbo's revision, how many are staff's uploads (never replaced), and those it would download.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetSyncKindPreview
{
    [Id(0)]
    public required AssetBundleKind Kind { get; init; }

    /// <summary>False when Habbo's list of this kind (the figure map, the effect map) could not be read.</summary>
    [Id(1)]
    public required bool Listed { get; init; }

    [Id(2)]
    public required int HabboLibraries { get; init; }

    [Id(3)]
    public required int UpToDate { get; init; }

    [Id(4)]
    public required int Uploads { get; init; }

    [Id(5)]
    public required int New { get; init; }

    [Id(6)]
    public required int Updated { get; init; }

    /// <summary>Those it would download, new ones first, up to the preview's limit.</summary>
    [Id(7)]
    public required ImmutableArray<AssetSyncPendingItem> Items { get; init; }

    [Id(8)]
    public required bool Truncated { get; init; }
}
