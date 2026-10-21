using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
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
        SentToRoom(harness)
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
        SentToRoom(harness)
            .Should()
            .Equal(
                nameof(RoomInfoUpdatedMessageComposer),
                nameof(RoomVisualizationSettingsMessageComposer),
                nameof(RoomChatSettingsMessageComposer)
            );
    }

    private static IEnumerable<string> SentToRoom(LiveRoomHarness harness) =>
        harness
            .Fakes.Log.Of("OnNextAsync")
            .SelectMany(c => ((RoomOutboundSnapshot)c.Args[0]!).Composers)
            .Select(c => c.GetType().Name);

    private static async Task<LiveRoomHarness> OwnedRoomAsync()
    {
        var harness = new LiveRoomHarness();
        var loaded = RoomHarness.GetMember(harness.State, "RoomSnapshot")!;
        RoomHarness.SetMember(loaded, "OwnerId", (PlayerId)OWNER);
        RoomHarness.SetMember(loaded, "Name", "Party");
        RoomHarness.SetMember(loaded, "Description", string.Empty);
        RoomHarness.SetMember(loaded, "Password", string.Empty);
        RoomHarness.SetMember(loaded, "Tags", ImmutableArray<string>.Empty);
        RoomHarness.SetMember(loaded, "ChatProtection", ChatFloodSensitivityType.Minimal);
        RoomHarness.SetMember(loaded, "AllowPets", true);
        RoomHarness.SetMember(loaded, "AllowPetsEat", true);
        RoomHarness.SetMember(loaded, "PlayersMax", 25);
        RoomHarness.SetMember(
            loaded,
            "ModSettings",
            Turbo.Primitives.Rooms.Snapshots.Settings.ModSettingsSnapshot.OwnerOnly
        );

        var db = (InMemoryDb)RoomHarness.GetField(harness.Room, "_dbCtxFactory")!;
        await using (var ctx = await db.CreateDbContextAsync(Ct))
        {
            ctx.Rooms.Add(
                new RoomEntity
                {
                    Id = 1,
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
