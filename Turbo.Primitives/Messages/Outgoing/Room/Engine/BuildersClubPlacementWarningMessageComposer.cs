using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

/// <summary>
/// Asks a trial or grace member to confirm that borrowing this furni will hide their room from
/// the navigator. The client shows <c>room.confirm.hide_room</c> and, on OK, re-sends the very
/// same placement request with its confirmation flag set, so everything here is echoed back from
/// what arrived rather than resolved again.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record BuildersClubPlacementWarningMessageComposer : IComposer
{
    [Id(0)]
    public required BuildersClubPlacementType PlacementType { get; init; }

    /// <summary>As the client sent it, which is -1 when the request came from an info stand.</summary>
    [Id(1)]
    public required int PageId { get; init; }

    [Id(2)]
    public required int OfferId { get; init; }

    [Id(3)]
    public required string ExtraParam { get; init; }

    [Id(4)]
    public int X { get; init; }

    [Id(5)]
    public int Y { get; init; }

    [Id(6)]
    public int Direction { get; init; }

    /// <summary>Only read for a wall item.</summary>
    [Id(7)]
    public string WallLocation { get; init; } = string.Empty;
}
