using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.WiredTrading.Snapshots;

/// <summary>
/// What the chest furni knows about itself and its grain needs on each call: the room keeps a
/// chest's settings in its stuff data, and the grain, which outlives a placement, reads them
/// from whoever calls rather than keeping a copy that could go stale.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredChestSettingsSnapshot
{
    [Id(0)]
    public required WiredChestKind Kind { get; init; }

    [Id(1)]
    public required PlayerId OwnerId { get; init; }

    /// <summary>The room the chest stands in, for the transaction log.</summary>
    [Id(2)]
    public required RoomId RoomId { get; init; }

    [Id(3)]
    public required bool IsStarter { get; init; }

    [Id(4)]
    public required WiredChestPreviewMode PreviewMode { get; init; }

    [Id(5)]
    public required int PreviewAmount { get; init; }
}
