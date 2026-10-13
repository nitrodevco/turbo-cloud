using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Commands;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>
/// <c>:pickall</c>, <c>:pickallbc</c> and <c>:ejectall</c>: the Flash client asks its own
/// confirm and then sends the line as chat (<c>SessionDataManager.pickAllFurniture</c>,
/// <c>pickAllBuilderFurniture</c>, <c>ejectAllFurniture</c>), and the server had no such
/// command, so after the confirm nothing happened.
/// </summary>
public class FurniCommandsTests
{
    private const int OWNER = 1;
    private const int BUILDER = 2;
    private const int VISITOR = 3;

    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly CommandRoomFixture _room = new();

    public FurniCommandsTests()
    {
        RoomHarness.SetMember(_room.Harness.State, "IsRightsLoaded", true);
        _room.Commands.Register([
            new PickAllCommand(),
            new PickAllBuildersClubCommand(),
            new EjectAllCommand(),
        ]);
    }

    [Fact]
    public async Task Pickall_takes_back_only_the_executors_own_furni()
    {
        _room.AddPlayer(BUILDER, PermissionNodes.Command.PICKALL);
        _room.GiveRights(BUILDER);
        AddFloorItem(10, 1, 1, BUILDER);
        AddFloorItem(11, 2, 1, BUILDER);
        AddFloorItem(12, 3, 1, OWNER);
        AddFloorItem(FurniIdBands.BuildersClubMin + 5, 4, 1, BUILDER);

        await _room.SayAsync(BUILDER, ":pickall");

        ItemIds().Should().BeEquivalentTo([12, FurniIdBands.BuildersClubMin + 5]);
        ReturnedTo(BUILDER).Should().BeEquivalentTo([10, 11]);
        ReturnedTo(OWNER).Should().BeEmpty();
        _room.SaidToRoom().Should().BeEmpty("the line is a command, not chat");
        _room.WhispersTo(BUILDER).Should().BeEmpty("the furni leaving is the answer");
    }

    [Fact]
    public async Task Pickall_needs_no_rights_for_the_players_own_furni()
    {
        _room.AddPlayer(VISITOR, PermissionNodes.Command.PICKALL);
        AddFloorItem(10, 1, 1, VISITOR);
        AddFloorItem(11, 2, 1, OWNER);

        await _room.SayAsync(VISITOR, ":pickall");

        ItemIds().Should().BeEquivalentTo([11]);
        ReturnedTo(VISITOR).Should().BeEquivalentTo([10]);
    }

    [Fact]
    public async Task Pickallbc_gives_back_only_the_executors_borrowed_furni()
    {
        var borrowed = FurniIdBands.BuildersClubMin + 5;
        var othersBorrowed = FurniIdBands.BuildersClubMin + 6;
        _room.AddPlayer(BUILDER, PermissionNodes.Command.PICKALLBC);
        _room.GiveRights(BUILDER);
        AddFloorItem(10, 1, 1, BUILDER);
        AddFloorItem(borrowed, 2, 1, BUILDER);
        AddFloorItem(othersBorrowed, 3, 1, OWNER);

        await _room.SayAsync(BUILDER, ":pickallbc");

        ItemIds().Should().BeEquivalentTo([10, othersBorrowed]);
        ReturnedTo(BUILDER).Should().BeEmpty("a borrowed furni goes back to the club");
        _room
            .Harness.Fakes.Log.Of("OnReturnedAsync")
            .Should()
            .ContainSingle()
            .Which.Args.Take(2)
            .Should()
            .Equal((Turbo.Primitives.Players.PlayerId)BUILDER, 1);
    }

    [Fact]
    public async Task Ejectall_by_the_owner_sends_everybody_elses_furni_home()
    {
        MakeGroupRoom();
        _room.AddPlayer(OWNER, PermissionNodes.Command.EJECTALL);
        AddFloorItem(10, 1, 1, BUILDER);
        AddFloorItem(11, 2, 1, VISITOR);
        AddFloorItem(12, 3, 1, OWNER);

        await _room.SayAsync(OWNER, ":ejectall");

        ItemIds().Should().BeEquivalentTo([12]);
        ReturnedTo(BUILDER).Should().BeEquivalentTo([10]);
        ReturnedTo(VISITOR).Should().BeEquivalentTo([11]);
        ReturnedTo(OWNER).Should().BeEmpty();
        _room.WhispersTo(OWNER).Should().BeEmpty();
    }

    [Fact]
    public async Task Ejectall_is_refused_to_a_player_with_only_rights()
    {
        MakeGroupRoom();
        _room.AddPlayer(BUILDER, PermissionNodes.Command.EJECTALL);
        _room.GiveRights(BUILDER);
        AddFloorItem(10, 1, 1, VISITOR);

        await _room.SayAsync(BUILDER, ":ejectall");

        ItemIds().Should().BeEquivalentTo([10]);
        _room
            .WhispersTo(BUILDER)
            .Should()
            .Equal("You can't eject other people's furni in this room.");
    }

    [Fact]
    public async Task Ejectall_only_works_in_a_group_room()
    {
        _room.AddPlayer(OWNER, PermissionNodes.Command.EJECTALL);
        AddFloorItem(10, 1, 1, BUILDER);

        await _room.SayAsync(OWNER, ":ejectall");

        ItemIds().Should().BeEquivalentTo([10], "Habbo: \"Eject all furniture in a group room\"");
        ReturnedTo(BUILDER).Should().BeEmpty();
        _room.WhispersTo(OWNER).Should().Equal(":ejectall only works in a group room.");
    }

    /// <summary>The room is group 9's homeroom.</summary>
    private void MakeGroupRoom()
    {
        var guild = (GuildSummarySnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(GuildSummarySnapshot));

        RoomHarness.SetMember(guild, "GuildId", (GuildId)9);
        _room.Harness.Fakes.Handlers["GetGuildOfRoomAsync"] = _ =>
            Task.FromResult<GuildSummarySnapshot?>(guild);
    }

    private void AddFloorItem(int id, int x, int y, int ownerId)
    {
        var stuffData = _room.Harness.Services.GetRequiredService<IStuffDataFactory>();
        var item = new RoomFloorItem
        {
            ObjectId = id,
            OwnerId = ownerId,
            OwnerName = $"player{ownerId}",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 1000,
                SpriteId = 1000,
                Name = "chair",
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = "default_floor",
                TotalStates = 2,
                Width = 1,
                Length = 1,
                StackHeight = Altitude.FromInt(100),
                CanStack = true,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Everybody,
                ExtraData = null,
            },
        };

        item.SetExtraData(null);
        item.SetPosition(x, y);
        item.SetPositionZ(Altitude.Zero);
        item.SetRotation(Rotation.North);
        item.SetLogic(
            new Turbo.Rooms.Object.Logic.Furniture.Floor.FurnitureFloorLogic(
                stuffData,
                new RoomFloorItemContext(_room.Harness.Room, item)
            )
        );

        ((IDictionary)RoomHarness.GetMember(_room.Harness.State, "ItemsById")!).Add(
            (RoomObjectId)id,
            item
        );
        typeof(RoomMapModule)
            .GetMethod("AddItem", All, [typeof(IRoomItem)])!
            .Invoke(_room.Harness.Module<RoomMapModule>(), [item]);
    }

    private IEnumerable<int> ItemIds() =>
        ((IDictionary)RoomHarness.GetMember(_room.Harness.State, "ItemsById")!)
            .Keys.Cast<RoomObjectId>()
            .Select(x => x.Value);

    /// <summary>The furni each inventory took back, by object id.</summary>
    private IEnumerable<int> ReturnedTo(int playerId) =>
        _room
            .Harness.Fakes.Log.Of("AddFurnitureFromRoomItemSnapshotsAsync")
            .Where(c => c.Interface == typeof(IInventoryGrain) && Equals(c.Key, (long)playerId))
            .SelectMany(c =>
                (System.Collections.Immutable.ImmutableArray<RoomItemSnapshot>)c.Args[0]!
            )
            .Select(x => x.ObjectId.Value);
}
