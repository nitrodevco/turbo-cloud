using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Moderation;
using Turbo.Events;
using Turbo.Events.Registry;
using Turbo.Operations;
using Turbo.Primitives.Action;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Settings;
using Turbo.Rooms.Configuration;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// What a room's moderation does to a player, through the room grain, lands in their moderation
/// history: by whom, from where (the game, the panel, or the room itself), and until when. Room
/// kicks, mutes and bans were written to the server log at most, so nobody could see what had
/// been done to a player, and the flood protection kept nothing at all.
/// </summary>
public sealed class RoomModerationHistoryTests : IDisposable
{
    private const int ROOM = 1;
    private const int STAFF = 500;
    private const int OWNER = 1;

    private readonly SqliteDb _db = new();
    private readonly WiredRoom _room = new(8, 8);
    private readonly int _alice;
    private readonly int _bob;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomModerationHistoryTests()
    {
        _alice = _room.Enter(1, 0, 0).PlayerId;
        _bob = _room.Enter(2, 1, 0).PlayerId;

        // A room mute and a room ban are rows on the room and the player.
        _db.InsertOwnersAndRooms([1, _alice, _bob, STAFF], [ROOM]);
        RoomHarness.SetField(_room.Harness.Room, "_dbCtxFactory", _db);

        RoomHarness.SetField(
            _room.Harness.Room,
            "_moderationLog",
            new ModerationLog(_db, NullLogger<IModerationLog>.Instance)
        );

        RoomHarness.SetMember(
            RoomHarness.GetMember(_room.Harness.State, "RoomSnapshot")!,
            "ModSettings",
            ModSettingsSnapshot.OwnerOnly
        );
        // Staff moderate every room, from anywhere.
        _room.Harness.Fakes.Handlers["HasAsync"] = call =>
            Task.FromResult(
                Convert.ToInt64(call.Key) == STAFF
                    && (string)call.Args[0]!
                        is PermissionNodes.Room.MODERATE_ANY
                            or PermissionNodes.Room.CONTROL_ANY
            );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_kick_is_recorded_with_who_did_it_and_from_where()
    {
        await _room.Harness.Room.KickPlayerAsync(
            ActionContext.CreateForPanel(STAFF, ROOM),
            _alice,
            Ct
        );
        await _room.Harness.Room.KickPlayerAsync(
            ActionContext.CreateForPlayer(STAFF, ROOM),
            _bob,
            Ct
        );

        History()
            .Select(x => (x.Action, x.Source, x.ActorId, x.TargetPlayerId, x.RoomId))
            .Should()
            .Equal(
                (ModerationActionType.RoomKick, ModerationSource.Panel, STAFF, _alice, ROOM),
                (ModerationActionType.RoomKick, ModerationSource.Game, STAFF, _bob, ROOM)
            );
    }

    [Fact]
    public async Task A_mute_and_a_ban_say_until_when_and_a_refused_one_is_not_recorded()
    {
        await _room.Harness.Room.MutePlayerAsync(
            ActionContext.CreateForPanel(STAFF, ROOM),
            _alice,
            10,
            Ct
        );
        await _room.Harness.Room.BanPlayerAsync(
            ActionContext.CreateForPanel(STAFF, ROOM),
            _bob,
            RoomBanDurationType.Hour,
            Ct
        );
        // Alice may not kick Bob: she holds nothing here.
        await _room.Harness.Room.KickPlayerAsync(
            ActionContext.CreateForPlayer(_alice, ROOM),
            _bob,
            Ct
        );

        var history = History();
        history
            .Select(x => (x.Action, x.TargetPlayerId, x.Detail))
            .Should()
            .Equal(
                (ModerationActionType.RoomMute, _alice, null),
                (ModerationActionType.RoomBan, _bob, "Hour")
            );
        history[0]
            .ExpiresAt.Should()
            .BeCloseTo(DateTime.UtcNow.AddMinutes(10), TimeSpan.FromMinutes(1));
        history[1]
            .ExpiresAt.Should()
            .BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task A_wired_kick_is_the_rooms_own_and_a_room_cleared_by_staff_is_one_record()
    {
        await _room.Harness.Room.ModerationModule.KickPlayerBySystemAsync(_alice, "", Ct);
        await _room.Harness.Room.ClearRoomBySystemAsync(
            ActionContext.CreateForPanel(STAFF, ROOM),
            Ct
        );

        History()
            .Select(x => (x.Action, x.Source, x.ActorId, x.TargetPlayerId, x.Reason, x.Detail))
            .Should()
            .Equal(
                (
                    ModerationActionType.RoomKick,
                    ModerationSource.System,
                    null,
                    _alice,
                    "wired",
                    null
                ),
                (ModerationActionType.RoomKickAll, ModerationSource.Panel, STAFF, null, null, "1")
            );
    }

    [Fact]
    public async Task Tripping_the_flood_protection_is_recorded_against_the_player()
    {
        RoomHarness.SetField(
            _room.Harness.Room,
            "_eventSystem",
            new EventSystem(
                new EventRegistry(_room.Harness.Services, NullLogger<EventRegistry>.Instance)
            )
        );
        RoomHarness.SetField(
            _room.Harness.Room,
            "_roomConfig",
            new RoomConfig { ChatlogEnabled = false, ChatFloodMaxMessagesNormalSensitivity = 2 }
        );
        RoomHarness.SetMember(
            RoomHarness.GetMember(_room.Harness.State, "RoomSnapshot")!,
            "ChatProtection",
            ChatFloodSensitivityType.Normal
        );

        for (var line = 0; line < 3; line++)
            await _room.Harness.Room.SendChatFromPlayerAsync(
                ActionContext.CreateForPlayer(_alice, ROOM),
                RoomChatType.Chat,
                "spam",
                0,
                Ct
            );

        History()
            .Select(x => (x.Action, x.Source, x.ActorId, x.TargetPlayerId))
            .Should()
            .Equal((ModerationActionType.RoomFloodMute, ModerationSource.System, null, _alice));
    }

    /// <summary>
    /// Rights taken away, by the owner or by staff, wrote nothing down: a player who lost their
    /// rights could not be told who took them.
    /// </summary>
    [Fact]
    public async Task Rights_taken_away_are_recorded_one_player_or_all()
    {
        foreach (var (id, player) in new[] { (1, _alice), (2, _bob) })
            _db.Insert(
                new Turbo.Database.Entities.Room.RoomRightEntity
                {
                    Id = id,
                    RoomEntityId = ROOM,
                    PlayerEntityId = player,
                }
            );
        // The security module was given the room's database when the room was built.
        RoomHarness.SetField(_room.Harness.Room.SecurityModule, "_dbCtxFactory", _db);

        // As the room loads them.
        var rights =
            (HashSet<Turbo.Primitives.Players.PlayerId>)
                RoomHarness.GetMember(_room.Harness.State, "PlayerIdsWithRights")!;
        rights.Add(_alice);
        rights.Add(_bob);

        await _room.Harness.Room.RemoveRightsFromPlayerAsync(
            ActionContext.CreateForPlayer(OWNER, ROOM),
            _alice,
            Ct
        );
        await _room.Harness.Room.RemoveAllRightsAsync(
            ActionContext.CreateForPanel(STAFF, ROOM),
            Ct
        );

        History()
            .Select(x => (x.Action, x.Source, x.ActorId, x.TargetPlayerId, x.Detail))
            .Should()
            .Equal(
                (
                    ModerationActionType.RoomRightsRemoved,
                    ModerationSource.Game,
                    OWNER,
                    _alice,
                    null
                ),
                (
                    ModerationActionType.RoomRightsRemovedAll,
                    ModerationSource.Panel,
                    STAFF,
                    null,
                    "1"
                )
            );
    }

    /// <summary>
    /// Staff changing a room that is not theirs is recorded with what changed (never the
    /// password itself); the owner changing their own room is not moderation and is not.
    /// </summary>
    [Fact]
    public async Task Staff_changing_a_rooms_settings_is_recorded_with_what_changed()
    {
        var snapshot = (RoomSnapshot)RoomHarness.GetMember(_room.Harness.State, "RoomSnapshot")!;
        RoomHarness.SetMember(
            _room.Harness.State,
            "RoomSnapshot",
            snapshot with
            {
                Name = "room",
                Description = "",
                Password = "",
                Tags = [],
                DoorMode = RoomDoorModeType.Open,
                PlayersMax = 25,
                CategoryId = -1,
                ChatProtection = ChatFloodSensitivityType.Normal,
            }
        );

        (
            await _room.Harness.Room.SaveRoomSettingsAsync(
                ActionContext.CreateForPanel(STAFF, ROOM),
                Settings("tidied up", RoomDoorModeType.Password, "secret"),
                Ct
            )
        )
            .Error.Should()
            .Be(RoomSettingsSaveErrorType.None);
        await _room.Harness.Room.SaveRoomSettingsAsync(
            ActionContext.CreateForPlayer(OWNER, ROOM),
            Settings("mine again", RoomDoorModeType.Open, ""),
            Ct
        );

        History()
            .Should()
            .ContainSingle()
            .Which.Should()
            .Match<ModerationActionEntity>(x =>
                x.Action == ModerationActionType.RoomSettingsChanged
                && x.Source == ModerationSource.Panel
                && x.ActorId == STAFF
                && x.Detail == "name \"tidied up\", door Password, password"
            );
    }

    private static RoomSettingsUpdateSnapshot Settings(
        string name,
        RoomDoorModeType door,
        string password
    ) =>
        new()
        {
            Name = name,
            Description = "",
            DoorMode = (int)door,
            Password = password,
            MaximumVisitors = 25,
            CategoryId = null,
            Tags = [],
            TradeMode = RoomTradeModeType.Disabled,
            AllowPets = false,
            AllowPetsEat = false,
            AllowWalkThrough = false,
            HideWalls = false,
            WallThickness = RoomThicknessType.Normal,
            FloorThickness = RoomThicknessType.Normal,
            WhoCanMute = ModSettingType.Owner,
            WhoCanKick = ModSettingType.Owner,
            WhoCanBan = ModSettingType.Owner,
            ChatProtection = ChatFloodSensitivityType.Normal,
            LeaveOnDoorTile = false,
            IdleSleepEnabled = false,
            IdleSleepTimeoutSeconds = 0,
            IdleAutokickEnabled = false,
            IdleAutokickTimeoutSeconds = 0,
            MuteAllPets = false,
        };

    private List<ModerationActionEntity> History()
    {
        using var db = _db.CreateDbContext();

        return [.. db.ModerationActions.OrderBy(x => x.Id)];
    }
}
