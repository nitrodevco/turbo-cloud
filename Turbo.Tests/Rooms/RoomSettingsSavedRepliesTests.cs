using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Configuration;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// What the room hears after its owner saves the settings dialog. Official client capture
/// 2026-10-13, two category and trade saves that changed neither the walls nor the chat:
/// RoomSettingsSaved, RoomInfoUpdated, RoomVisualizationSettings, RoomChatSettings, in the same
/// millisecond both times. Turbo sent the look and chat rules only when they changed, and before
/// RoomInfoUpdated.
/// </summary>
public class RoomSettingsSavedRepliesTests
{
    private const int OWNER = 1;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_category_and_trade_save_is_followed_by_the_room_info_look_and_chat_rules()
    {
        var harness = await OwnedRoomAsync();

        var result = await harness.Room.UpdateCategoryAndTradeSettingsAsync(
            ActionContext.CreateForPlayer(OWNER, new RoomId(1)),
            null,
            RoomTradeModeType.Everyone,
            Ct
        );

        result.Error.Should().Be(RoomSettingsSaveErrorType.None);
        harness
            .Fakes.SentToRoom<IComposer>()
            .Select(c => c.GetType().Name)
            .Should()
            .Equal(
                nameof(RoomInfoUpdatedMessageComposer),
                nameof(RoomVisualizationSettingsMessageComposer),
                nameof(RoomChatSettingsMessageComposer)
            );
    }

    [Fact]
    public async Task A_settings_dialog_save_is_followed_by_the_room_info_look_and_chat_rules()
    {
        var harness = await OwnedRoomAsync();

        var result = await harness.Room.SaveRoomSettingsAsync(
            ActionContext.CreateForPlayer(OWNER, new RoomId(1)),
            new Turbo.Primitives.Rooms.Snapshots.Settings.RoomSettingsUpdateSnapshot
            {
                Name = "Party",
                Description = "",
                DoorMode = (int)RoomDoorModeType.Open,
                Password = "",
                MaximumVisitors = 25,
                CategoryId = null,
                Tags = [],
                TradeMode = RoomTradeModeType.Disabled,
                AllowPets = true,
                AllowPetsEat = true,
                AllowWalkThrough = false,
                HideWalls = false,
                WallThickness = RoomThicknessType.Normal,
                FloorThickness = RoomThicknessType.Normal,
                WhoCanMute = ModSettingType.Owner,
                WhoCanKick = ModSettingType.Owner,
                WhoCanBan = ModSettingType.Owner,
                ChatProtection = ChatFloodSensitivityType.Minimal,
                LeaveOnDoorTile = false,
                IdleSleepEnabled = false,
                IdleSleepTimeoutSeconds = 0,
                IdleAutokickEnabled = false,
                IdleAutokickTimeoutSeconds = 0,
                MuteAllPets = false,
            },
            Ct
        );

        result.Error.Should().Be(RoomSettingsSaveErrorType.None);
        harness
            .Fakes.SentToRoom<IComposer>()
            .Select(c => c.GetType().Name)
            .Should()
            .Equal(
                nameof(RoomInfoUpdatedMessageComposer),
                nameof(RoomVisualizationSettingsMessageComposer),
                nameof(RoomChatSettingsMessageComposer)
            );
    }

    [Fact]
    public async Task Tags_from_the_room_info_keep_to_the_rooms_own_limits()
    {
        using var db = new SqliteDb();
        db.Insert(
            new PlayerEntity
            {
                Id = OWNER,
                Name = "owner",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model",
                Model = "00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );
        var harness = await OwnedRoomAsync(db);
        var limits = new RoomConfig();

        // The client sends two; a packet can carry anything.
        await harness.Room.SetTagsAsync(
            ActionContext.CreateForPlayer(OWNER, new RoomId(1)),
            ["party", new string('x', limits.RoomTagMaxLength + 1), "chill", "games"],
            Ct
        );

        await using var ctx = await harness.Room._dbCtxFactory.CreateDbContextAsync(Ct);
        var tags = RoomTags.Parse((await ctx.Rooms.FindAsync([1], Ct))!.Tags);

        tags.Should().HaveCount(limits.RoomTagsMax);
        tags.Should().OnlyContain(x => x.Length <= limits.RoomTagMaxLength);
    }

    private static async Task<LiveRoomHarness> OwnedRoomAsync(SqliteDb? sqlite = null)
    {
        var harness = new LiveRoomHarness(db: sqlite);
        harness.State.RoomSnapshot = harness.State.RoomSnapshot with
        {
            OwnerId = (PlayerId)OWNER,
            Name = "Party",
            Description = string.Empty,
            Password = string.Empty,
            Tags = ImmutableArray<string>.Empty,
            ChatProtection = ChatFloodSensitivityType.Minimal,
            AllowPets = true,
            AllowPetsEat = true,
            PlayersMax = 25,
            ModSettings = Turbo.Primitives.Rooms.Snapshots.Settings.ModSettingsSnapshot.OwnerOnly,
        };

        var db = harness.Room._dbCtxFactory;
        await using (var ctx = await db.CreateDbContextAsync(Ct))
        {
            ctx.Rooms.Add(
                new RoomEntity
                {
                    Id = 1,
                    LastActive = ManualTimeProvider.FixedStart.UtcDateTime,
                    Name = "Party",
                    PlayerEntityId = OWNER,
                    RoomModelEntityId = 1,
                    DoorMode = RoomDoorModeType.Open,
                    UsersNow = 0,
                    PlayersMax = 25,
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
            await ctx.SaveChangesAsync(Ct);
        }

        return harness;
    }
}
