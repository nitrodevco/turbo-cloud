using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots;

/// <summary>
/// How a player is arriving in a room. A furni that sends someone to another room says so
/// before it forwards them, and the room they land in keeps it on their avatar for the wired
/// <c>@room_entry.*</c> variables to read.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RoomEntrySnapshot
{
    public static RoomEntrySnapshot Default { get; } =
        new() { Method = RoomEntryMethodType.Default, TeleportId = 0 };

    [Id(0)]
    public required RoomEntryMethodType Method { get; init; }

    /// <summary>
    /// The teleporter they arrive at, when they came through one; zero otherwise. It is the
    /// furni in this room, the far half of the pair they stepped into.
    /// </summary>
    [Id(1)]
    public required int TeleportId { get; init; }

    /// <summary>
    /// The room they came from when a furni or a room link sent them (<c>@event.link.source_room_id</c>);
    /// zero for every other way in.
    /// </summary>
    [Id(2)]
    public RoomId SourceRoomId { get; init; }

    /// <summary>
    /// What a plugin's furni sent with the player (<see cref="RoomEntryExtension"/>), for the
    /// arrival policies of the room they land in to read; null for every other way in. It changes
    /// nothing about the way in itself: the room's door, its bans and its entry policies decide as
    /// they do for a plain entry.
    /// </summary>
    [Id(3)]
    public RoomEntryExtension? Extension { get; init; }

    /// <summary>
    /// They come through a plugin furni's linked pair, into the room its other half stands in:
    /// set by core alone (<c>IFurniContext.ForwardToRoomAsync</c> checks the pair), never by the
    /// plugin. It passes the room's door (doorbell, password) as a teleporter does, since both
    /// halves were placed by someone with rights in both rooms; bans, capacity and the entry
    /// policies still decide. Unlike a teleporter's arrival the room's arrival policies still
    /// place them, and the entry method stays what <see cref="Method"/> says.
    /// </summary>
    [Id(4)]
    public bool ThroughLinkedPair { get; init; }

    /// <summary>
    /// Whether this way in passes the room's door (doorbell, password): a teleporter's far half,
    /// or a linked pair's (<see cref="ThroughLinkedPair"/>). Bans and capacity still apply.
    /// </summary>
    public bool PassesDoor => Method == RoomEntryMethodType.Teleport || ThroughLinkedPair;
}

/// <summary>
/// A plugin's own data riding with a player into another room: its key (the plugin's, so two
/// plugins can tell theirs apart) and a payload the plugin serialised itself. Strings only, so it
/// crosses grains while the plugin's own types stay in the plugin.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RoomEntryExtension(
    [property: Id(0)] string Key,
    [property: Id(1)] string Payload
);
