using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Settings;
using Turbo.Rooms.Configuration;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room's raid protection, as its window reads and saves it. Only the owner may manage it.
/// Every value must be one the client's menus offer, and turning protection on needs the
/// player's confirmation (<c>raid.protection.settings.save.fail.4</c> and <c>.5</c>). A saved
/// setting is what the window shows next time.
/// </summary>
public sealed class RaidProtectionSettingsTests : IDisposable
{
    private const int OWNER = 1;
    private const int GUEST = 5;
    private const int ROOM = 1;

    private static readonly ActionContext Owner = ActionContext.CreateForPlayer(OWNER, ROOM);
    private static readonly ActionContext Guest = ActionContext.CreateForPlayer(GUEST, ROOM);

    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RaidProtectionSettingsTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = OWNER,
                Name = "owner",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model_a",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );
        _db.Insert(
            new RoomEntity
            {
                Id = ROOM,
                Name = "room",
                PlayerEntityId = OWNER,
                RoomModelEntityId = 1,
                DoorMode = RoomDoorModeType.Open,
                UsersNow = 0,
                PlayersMax = 25,
                PaintWall = "101",
                WallHeight = -1,
                HideWalls = false,
                ThicknessWall = RoomThicknessType.Normal,
                ThicknessFloor = RoomThicknessType.Normal,
                AllowBlocking = false,
                AllowPets = true,
                AllowPetsEat = true,
                TradeType = RoomTradeModeType.Disabled,
                MuteType = ModSettingType.Owner,
                KickType = ModSettingType.Owner,
                BanType = ModSettingType.Owner,
                ChatFloodType = ChatFloodSensitivityType.Minimal,
                PlayerEntity = null!,
                RoomModelEntity = null!,
            }
        );
    }

    public void Dispose() => _db.Dispose();

    /// <summary>What Habbo's window shows for a room never set up (evidence raid.png).</summary>
    [Fact]
    public async Task A_room_never_set_up_shows_habbos_defaults_with_no_raid()
    {
        var settings = await Room().Room.GetRaidProtectionSettingsAsync(Owner, Ct);

        settings
            .Should()
            .BeEquivalentTo(
                new RaidProtectionSettingsSnapshot
                {
                    RoomId = ROOM,
                    Enabled = false,
                    DetectionSensitivity = RaidSensitivityType.Medium,
                    ActionType = RaidActionType.TemporaryBan,
                    BanDurationSeconds = 900,
                    GuardEnabled = false,
                    GuardDurationSeconds = 900,
                    GuardSensitivity = RaidSensitivityType.Medium,
                    IncidentActive = false,
                    LastRaidAtEpochSeconds = 0,
                }
            );
    }

    [Fact]
    public async Task Only_the_owner_may_manage_it()
    {
        var room = Room();

        (await room.Room.CanManageRaidProtectionAsync(Owner, Ct)).Should().BeTrue();
        (await room.Room.CanManageRaidProtectionAsync(Guest, Ct)).Should().BeFalse();
        (await room.Room.GetRaidProtectionSettingsAsync(Guest, Ct)).Should().BeNull();
        (await room.Room.SaveRaidProtectionSettingsAsync(Guest, Update(), Ct))
            .Result.Should()
            .Be(RaidProtectionSaveResultType.NotAllowed);
        (await Rows()).Should().BeEmpty();
    }

    [Fact]
    public async Task Turning_it_on_needs_the_confirmation()
    {
        var room = Room();

        var unconfirmed = await room.Room.SaveRaidProtectionSettingsAsync(
            Owner,
            Update() with
            {
                Confirmed = false,
            },
            Ct
        );

        unconfirmed.Result.Should().Be(RaidProtectionSaveResultType.NotConfirmed);
        unconfirmed.Settings.Enabled.Should().BeFalse();
        (await Rows()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(3, 1, 900, 900, 1)]
    [InlineData(1, 2, 900, 900, 1)]
    [InlineData(1, 1, 901, 900, 1)]
    [InlineData(1, 1, 900, 86400, 1)]
    [InlineData(1, 1, 900, 900, -1)]
    public async Task A_value_no_menu_offers_is_refused(
        int sensitivity,
        int action,
        int banSeconds,
        int guardSeconds,
        int guardSensitivity
    )
    {
        var result = await Room()
            .Room.SaveRaidProtectionSettingsAsync(
                Owner,
                Update() with
                {
                    DetectionSensitivity = sensitivity,
                    ActionType = action,
                    BanDurationSeconds = banSeconds,
                    GuardDurationSeconds = guardSeconds,
                    GuardSensitivity = guardSensitivity,
                },
                Ct
            );

        result.Result.Should().Be(RaidProtectionSaveResultType.Invalid);
        (await Rows()).Should().BeEmpty();
    }

    [Fact]
    public async Task A_confirmed_save_is_what_the_window_shows_next_and_can_be_changed_again()
    {
        var room = Room();

        var saved = await room.Room.SaveRaidProtectionSettingsAsync(Owner, Update(), Ct);

        saved.Result.Should().Be(RaidProtectionSaveResultType.Saved);
        saved.Settings.Enabled.Should().BeTrue();

        var shown = await room.Room.GetRaidProtectionSettingsAsync(Owner, Ct);

        shown!.Enabled.Should().BeTrue();
        shown.ActionType.Should().Be(RaidActionType.TemporaryBan);
        shown.BanDurationSeconds.Should().Be(86400);
        shown.GuardEnabled.Should().BeTrue();
        shown.GuardDurationSeconds.Should().Be(3600);
        shown.GuardSensitivity.Should().Be(RaidSensitivityType.High);

        // Already on, a change needs no second confirmation.
        (
            await room.Room.SaveRaidProtectionSettingsAsync(
                Owner,
                Update() with
                {
                    Confirmed = false,
                    DetectionSensitivity = (int)RaidSensitivityType.Low,
                },
                Ct
            )
        )
            .Result.Should()
            .Be(RaidProtectionSaveResultType.Saved);
        (await Rows())
            .Should()
            .ContainSingle()
            .Which.DetectionSensitivity.Should()
            .Be(RaidSensitivityType.Low);
    }

    /// <summary>
    /// Kick and low sensitivity are each their type's first member (0). A room's first save
    /// creates its row, and a 0 left out of that insert was stored as the column's default.
    /// </summary>
    [Fact]
    public async Task A_first_save_of_kick_at_low_sensitivity_is_what_the_window_shows_next()
    {
        var room = Room();

        var saved = await room.Room.SaveRaidProtectionSettingsAsync(
            Owner,
            Update() with
            {
                DetectionSensitivity = (int)RaidSensitivityType.Low,
                ActionType = (int)RaidActionType.Kick,
                GuardSensitivity = (int)RaidSensitivityType.Low,
            },
            Ct
        );

        saved.Result.Should().Be(RaidProtectionSaveResultType.Saved);

        var shown = await Room().Room.GetRaidProtectionSettingsAsync(Owner, Ct);

        shown!.DetectionSensitivity.Should().Be(RaidSensitivityType.Low);
        shown.ActionType.Should().Be(RaidActionType.Kick);
        shown.GuardSensitivity.Should().Be(RaidSensitivityType.Low);
    }

    [Fact]
    public async Task Nobody_may_manage_it_while_the_hotel_has_it_off()
    {
        var room = Room(new RoomConfig { RaidProtectionEnabled = false });

        (await room.Room.CanManageRaidProtectionAsync(Owner, Ct)).Should().BeFalse();
        (await room.Room.SaveRaidProtectionSettingsAsync(Owner, Update(), Ct))
            .Result.Should()
            .Be(RaidProtectionSaveResultType.FeatureDisabled);
    }

    private LiveRoomHarness Room(RoomConfig? config = null)
    {
        var room = new LiveRoomHarness(roomConfig: config);
        RoomHarness.SetField(room.Room, "_dbCtxFactory", _db);
        return room;
    }

    private async Task<List<RoomRaidProtectionEntity>> Rows()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);
        return await db.RoomRaidProtections.ToListAsync(Ct);
    }

    private static RaidProtectionSettingsUpdateSnapshot Update() =>
        new()
        {
            Enabled = true,
            DetectionSensitivity = (int)RaidSensitivityType.Medium,
            ActionType = (int)RaidActionType.TemporaryBan,
            BanDurationSeconds = 86400,
            GuardEnabled = true,
            GuardDurationSeconds = 3600,
            GuardSensitivity = (int)RaidSensitivityType.High,
            Confirmed = true,
        };
}
