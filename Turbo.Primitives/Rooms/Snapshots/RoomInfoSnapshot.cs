using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots;

[GenerateSerializer, Immutable]
public record RoomInfoSnapshot : RoomSummarySnapshot
{
    [Id(0)]
    public required RoomDoorModeType DoorMode { get; init; } = RoomDoorModeType.Invisible;

    [Id(1)]
    public required int PlayersMax { get; init; } = 0;

    [Id(2)]
    public required RoomTradeModeType TradeType { get; init; } = RoomTradeModeType.Disabled;

    [Id(3)]
    public required int Score { get; init; } = 0;

    [Id(4)]
    public required int Ranking { get; init; } = 0;

    [Id(5)]
    public required int CategoryId { get; init; } = -1;

    [Id(6)]
    public required ImmutableArray<string> Tags { get; init; } = [];

    [Id(7)]
    public required bool AllowBlocking { get; init; } = false;

    [Id(8)]
    public required bool AllowPets { get; init; } = false;

    [Id(9)]
    public required bool AllowPetsEat { get; init; } = false;

    [Id(10)]
    public bool StaffPick { get; init; } = false;

    [Id(11)]
    public RoomEventSnapshot? ActiveEvent { get; init; }

    /// <summary>
    /// The room is off the navigator and closed to everyone but its owner, because somebody
    /// borrowed Builders Club furni into it without a membership to keep it. It is a listing
    /// field rather than a room-only one because the navigator filters on it without loading
    /// the room.
    /// </summary>
    [Id(12)]
    public bool HiddenByBc { get; init; }

    /// <summary>
    /// The door the client draws on the room card: invisible while Builders Club hides the room,
    /// whatever its owner chose, which <see cref="DoorMode"/> keeps for when it is shown again.
    /// </summary>
    public RoomDoorModeType ListedDoorMode => HiddenByBc ? RoomDoorModeType.Invisible : DoorMode;

    /// <summary>
    /// The group whose homeroom this is, or null. It is a listing field because the client draws
    /// the badge on the room card as well as inside the room, and neither should have to load
    /// the group to do it.
    /// </summary>
    [Id(13)]
    public GuildSummarySnapshot? Guild { get; init; }

    /// <summary>
    /// The owner's name is left off the room's card and info (GuestRoomData's show-owner bit),
    /// as Habbo does for its official rooms: every recorded room of official_rooms has it off
    /// (84 of 84, official client capture, WIN63-202610131318), and so do a few players' rooms.
    /// </summary>
    [Id(14)]
    public bool HideOwner { get; init; }
}
