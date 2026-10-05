using FluentAssertions;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Rooms;
using Turbo.Database.Entities.Navigator;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots.Settings;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Saving a room's settings from the panel: through the room grain's own save, as the staff
/// member, after the two checks the game's packet handler makes first (the category, and a
/// password door's password the panel never sees).
/// </summary>
public sealed class AdminRoomEditorTests : IDisposable
{
    private const int OWNER = 1;
    private const int ROOM = 10;
    private const int STAFF_CATEGORY = 7;

    private static readonly PlayerId STAFF = new(50);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private RoomSettingsSaveResultSnapshot _answer = RoomSettingsSaveResultSnapshot.Success;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminRoomEditorTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = OWNER,
                Name = "alice",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Female,
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
            new NavigatorFlatCategoryEntity
            {
                Id = STAFF_CATEGORY,
                Name = "Staff rooms",
                Visible = true,
                Automatic = false,
                StaffOnly = true,
                MinRank = 7,
                OrderNum = 1,
            }
        );
        _db.Insert(
            new RoomEntity
            {
                Id = ROOM,
                Name = "Alice's Cafe",
                PlayerEntityId = OWNER,
                RoomModelEntityId = 1,
                DoorMode = RoomDoorModeType.Password,
                Password = "secret",
                UsersNow = 0,
                PlayersMax = 25,
                WallHeight = -1,
                HideWalls = false,
                ThicknessWall = RoomThicknessType.Normal,
                ThicknessFloor = RoomThicknessType.Normal,
                AllowBlocking = true,
                AllowPets = true,
                AllowPetsEat = true,
                TradeType = RoomTradeModeType.Disabled,
                MuteType = ModSettingType.Owner,
                KickType = ModSettingType.Owner,
                BanType = ModSettingType.Owner,
                ChatFloodType = ChatFloodSensitivityType.Minimal,
                LastActive = DateTime.UtcNow,
                PlayerEntity = null!,
                RoomModelEntity = null!,
            }
        );

        _fakes.Handlers["SaveRoomSettingsAsync"] = _ => Task.FromResult(_answer);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task TheSaveGoesThroughTheRoomItself_AsTheStaffMember()
    {
        var result = await Editor()
            .SaveSettingsAsync(STAFF, ROOM, Request() with { Name = "Cafe" }, Ct);

        result.Outcome.Should().Be(AdminRoomSaveOutcome.Saved);
        var (ctx, settings) = Saved();
        // The room checks the staff member owns it or holds room.control.any, as in the hotel.
        ctx.Origin.Should().Be(ActionOrigin.Player);
        ctx.PlayerId.Should().Be(STAFF);
        settings.Name.Should().Be("Cafe");
        settings.DoorMode.Should().Be((int)RoomDoorModeType.Open);
        settings.WhoCanKick.Should().Be(ModSettingType.Rights);
        settings.WallThickness.Should().Be(RoomThicknessType.Thin);
    }

    [Fact]
    public async Task APasswordDoor_KeepsItsPassword_WhenNoneIsTyped()
    {
        await Editor()
            .SaveSettingsAsync(
                STAFF,
                ROOM,
                Request() with
                {
                    DoorMode = "Password",
                    Password = "",
                },
                Ct
            );

        Saved().Settings.Password.Should().Be("secret");
    }

    [Fact]
    public async Task ATypedPassword_ReplacesTheOldOne()
    {
        await Editor()
            .SaveSettingsAsync(
                STAFF,
                ROOM,
                Request() with
                {
                    DoorMode = "Password",
                    Password = "new one",
                },
                Ct
            );

        Saved().Settings.Password.Should().Be("new one");
    }

    [Fact]
    public async Task StaffMayUseAStaffOnlyCategory_ButNotOneThatDoesNotExist()
    {
        (
            await Editor()
                .SaveSettingsAsync(STAFF, ROOM, Request() with { CategoryId = STAFF_CATEGORY }, Ct)
        )
            .Outcome.Should()
            .Be(AdminRoomSaveOutcome.Saved);

        var missing = await Editor()
            .SaveSettingsAsync(STAFF, ROOM, Request() with { CategoryId = 99 }, Ct);

        missing.Outcome.Should().Be(AdminRoomSaveOutcome.Invalid);
        missing.Message.Should().Contain("99");
        SaveCalls().Should().HaveCount(1, "the room is not asked to save an unknown category");
    }

    [Fact]
    public async Task AValueTheRoomDoesNotKnow_IsRefusedBeforeTheRoomIsAsked()
    {
        var result = await Editor()
            .SaveSettingsAsync(STAFF, ROOM, Request() with { WhoCanBan = "Everybody" }, Ct);

        result.Outcome.Should().Be(AdminRoomSaveOutcome.Invalid);
        SaveCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task TheRoomsOwnRefusals_AreExplained()
    {
        _answer = RoomSettingsSaveResultSnapshot.Failed(
            RoomSettingsSaveErrorType.TagTooLong,
            "averyveryverylongtagindeed"
        );

        var result = await Editor().SaveSettingsAsync(STAFF, ROOM, Request(), Ct);

        result.Outcome.Should().Be(AdminRoomSaveOutcome.Invalid);
        result.Message.Should().Contain("averyveryverylongtagindeed");
    }

    [Fact]
    public async Task AnUnknownRoom_IsNotFound()
    {
        (await Editor().SaveSettingsAsync(STAFF, 999, Request(), Ct))
            .Outcome.Should()
            .Be(AdminRoomSaveOutcome.NotFound);
        SaveCalls().Should().BeEmpty();
    }

    private AdminRoomEditor Editor() => new(_db, _fakes.Create<IGrainFactory>());

    private IEnumerable<FakeCall> SaveCalls() =>
        _fakes.Log.On<IRoomGrain>().Where(x => x.Method == "SaveRoomSettingsAsync");

    private (ActionContext Context, RoomSettingsUpdateSnapshot Settings) Saved()
    {
        var call = SaveCalls().Last();

        return ((ActionContext)call.Args[0]!, (RoomSettingsUpdateSnapshot)call.Args[1]!);
    }

    private static RoomSettingsRequest Request() =>
        new(
            Name: "Alice's Cafe",
            Description: "",
            DoorMode: "Open",
            Password: null,
            MaxPlayers: 25,
            CategoryId: null,
            Tags: ["cafe"],
            TradeMode: "Disabled",
            AllowPets: true,
            AllowPetsEat: true,
            AllowWalkThrough: true,
            HideWalls: false,
            WallThickness: "Thin",
            FloorThickness: "Normal",
            WhoCanMute: "Owner",
            WhoCanKick: "Rights",
            WhoCanBan: "Owner",
            ChatFloodProtection: "Normal",
            LeaveOnDoorTile: false,
            IdleSleepEnabled: false,
            IdleSleepTimeoutSeconds: 0,
            IdleAutokickEnabled: false,
            IdleAutokickTimeoutSeconds: 0,
            MuteAllPets: false
        );
}
