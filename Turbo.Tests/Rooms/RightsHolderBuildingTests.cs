using System.Collections;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Placing furni with plain room rights. On Habbo a player the owner gave rights (controller level
/// 1) in a room without a group could not place furni: seven refusals, four accounts, two
/// machines, one owner in Habbo Club, each answered with the <c>furni_placement_error</c> dialog
/// "${room.error.cant_set_not_owner}" (official client captures, PlaceObject 2026-10-13). They
/// still moved and turned the room's furni; a group's admin placed in its homeroom. Ours let every
/// rights holder place; the hotel now chooses, and Habbo's rule is the default.
/// </summary>
public sealed class RightsHolderBuildingTests
{
    private const int HOLDER_AVATAR = 2;
    private const int HOLDER = 102;
    private const int CRATE = 30;
    private const int NEW_ITEM = 31;
    private const string NOT_OWNER = "${room.error.cant_set_not_owner}";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task By_default_a_rights_holder_placing_furni_is_told_they_have_no_rights_to()
    {
        var room = CreateRoom(new RoomConfig());

        await PlaceAsync(room, HOLDER);

        HasItem(room, NEW_ITEM).Should().BeFalse();
        DialogMessages(room).Should().Equal(NOT_OWNER);
    }

    [Fact]
    public async Task A_rights_holder_places_furni_where_the_hotel_lets_rights_holders_place()
    {
        var room = CreateRoom(new RoomConfig { RightsHoldersCanPlaceFurni = true });

        await PlaceAsync(room, HOLDER);

        HasItem(room, NEW_ITEM).Should().BeTrue();
        DialogMessages(room).Should().BeEmpty();
    }

    [Fact]
    public async Task By_default_a_rights_holder_still_moves_and_turns_the_rooms_furni()
    {
        var room = CreateRoom(new RoomConfig());

        await MoveAsync(room, HOLDER);

        var crate = room.FloorItem(CRATE);
        (crate.X, crate.Y, crate.Rotation).Should().Be((5, 5, Rotation.East));
        DialogMessages(room).Should().BeEmpty();
    }

    [Fact]
    public async Task The_owner_still_places_by_default()
    {
        var room = CreateRoom(new RoomConfig());

        await PlaceAsync(room, 1);

        HasItem(room, NEW_ITEM).Should().BeTrue();
        DialogMessages(room).Should().BeEmpty();
    }

    [Theory]
    [InlineData(GuildMemberRank.Admin, GuildRightsLevel.Admins)]
    [InlineData(GuildMemberRank.Member, GuildRightsLevel.Members)]
    public async Task In_a_group_homeroom_the_group_still_decides_by_default(
        GuildMemberRank rank,
        GuildRightsLevel rightsLevel
    )
    {
        var room = CreateRoom(new RoomConfig());
        var guild = (GuildSummarySnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(GuildSummarySnapshot));

        RoomHarness.SetMember(guild, "GuildId", (GuildId)9);
        RoomHarness.SetMember(guild, "RightsLevel", rightsLevel);
        room.Harness.Fakes.Handlers["GetGuildOfRoomAsync"] = _ =>
            Task.FromResult<GuildSummarySnapshot?>(guild);
        room.Harness.Fakes.Handlers["GetMemberRankAsync"] = _ =>
            Task.FromResult<GuildMemberRank?>(rank);

        await PlaceAsync(room, HOLDER);

        HasItem(room, NEW_ITEM).Should().BeTrue();
        DialogMessages(room).Should().BeEmpty();
    }

    /// <summary>
    /// A room owned by player 1 with a crate of theirs at 6,6, and player 102 standing in it
    /// holding plain rights. The room service in front of it is the one the packets reach.
    /// </summary>
    private static WiredRoom CreateRoom(RoomConfig config)
    {
        var room = new WiredRoom(8, 8, config);
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        room.Harness.Fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(IPermissionRegistryProvider) ? registry : Fakes.NotHandled;
        room.Harness.Fakes.Instances[(typeof(IRoomGrain), 1L)] = room.Harness.Room;

        room.AddFloorItem(CRATE, 6, 6);
        room.Enter(HOLDER_AVATAR, 1, 1);
        ((ISet<PlayerId>)RoomHarness.GetMember(room.Harness.State, "PlayerIdsWithRights")!).Add(
            HOLDER
        );

        return room;
    }

    private static Task PlaceAsync(WiredRoom room, int playerId)
    {
        var definition = room.FloorItem(CRATE).Definition;

        room.Harness.Fakes.Handlers["GetItemSnapshotAsync"] = _ =>
            Task.FromResult<FurnitureItemSnapshot?>(
                new FurnitureItemSnapshot
                {
                    ItemId = NEW_ITEM,
                    SpriteId = definition.SpriteId,
                    OwnerId = playerId,
                    OwnerName = "placer",
                    Definition = definition,
                    StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "0" },
                    ExtraData = string.Empty,
                    SecondsToExpiration = -1,
                    HasRentPeriodStarted = false,
                    RoomId = -1,
                }
            );
        room.Harness.Fakes.Handlers["CreateFromFurnitureItemSnapshot"] = _ =>
        {
            var item = new RoomFloorItem
            {
                ObjectId = NEW_ITEM,
                OwnerId = playerId,
                OwnerName = "placer",
                Definition = definition,
            };
            item.SetExtraData(null);

            return item;
        };

        return Service(room)
            .PlaceFloorItemInRoomAsync(
                ActionContext.CreateForPlayer(playerId, 1),
                NEW_ITEM,
                3,
                3,
                Rotation.North,
                Ct
            );
    }

    private static Task MoveAsync(WiredRoom room, int playerId) =>
        Service(room)
            .MoveFloorItemInRoomAsync(
                ActionContext.CreateForPlayer(playerId, 1),
                CRATE,
                5,
                5,
                Rotation.East,
                Ct
            );

    private static IRoomService Service(WiredRoom room) =>
        (IRoomService)
            Activator.CreateInstance(
                typeof(RoomConfig).Assembly.GetType("Turbo.Rooms.RoomService")!,
                NullLogger<IRoomService>.Instance,
                Options.Create(new RoomConfig()),
                room.Grains
            )!;

    private static bool HasItem(WiredRoom room, int id) =>
        ((IDictionary)RoomHarness.GetMember(room.Harness.State, "ItemsById")!).Contains(
            (RoomObjectId)id
        );

    /// <summary>The <c>message</c> of every placement dialog the room service sent.</summary>
    private static IEnumerable<string> DialogMessages(WiredRoom room) =>
        room
            .Harness.Fakes.Log.Of("SendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<NotificationDialogMessageComposer>()
            .Select(x => x.Parameters["message"]);
}
