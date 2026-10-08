using System.Collections;
using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Crackable furni, driven through the room's use path as the client's double-click and the
/// infostand's Use button reach it: the Habbo Club and Builders Club boxes, multi-hit eggs,
/// effect-dependent plants, and the boxes left in their empty opening frame before the logic
/// existed (the "ghost blocks").
/// </summary>
public sealed class CrackableFurniTests
{
    private const int BOX = 50;
    private const int OWNER_AVATAR = 1;
    private const int GUEST_AVATAR = 2;
    private const int OWNER = 100 + OWNER_AVATAR;
    private const int GUEST = 100 + GUEST_AVATAR;

    private const string BC_BOX =
        """{"crackable":{"rewardSet":"bcgift_1","target":1,"hitBy":"Owner","subscription":"BuildersClub","subscriptionDays":14}}""";

    private const string EGG =
        """{"crackable":{"rewardSet":"egg_p1","target":4,"hitBy":"Anyone","incrementalHitAchievement":"EggCracker","finalHitAchievement":"EggMaster","subscription":"HabboClub","subscriptionDays":1}}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(10, 10);

    [Fact]
    public async Task A_bc_box_opens_on_its_owners_hit_and_gives_the_membership_once_it_is_gone()
    {
        var owner = _room.Enter(OWNER_AVATAR, 3, 4);
        var box = AddCrackable(BOX, 3, 3, OWNER, BC_BOX, totalStates: 3);

        await UseAsync(OWNER);

        // The cracking hit: the opening state (2), "Hits: 1 / 1", the box still there.
        Progress(box).Should().Be((1, 1, "2"));
        ItemExists().Should().BeTrue();
        Extensions().Should().BeEmpty();

        await RunTimersAsync();

        ItemExists().Should().BeFalse();
        Extensions()
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be((OWNER, SubscriptionType.BuildersClub, 14));
        owner.Should().NotBeNull();
    }

    [Fact]
    public async Task A_box_toggled_to_its_empty_opening_frame_before_comes_back_whole()
    {
        _room.Enter(OWNER_AVATAR, 3, 4);

        // How Dippy's BC boxes were stored: legacy stuff data in state 2, the asset's empty
        // opening frame, which drew only a shadow and blocked the tile.
        var box = AddCrackable(
            BOX,
            3,
            3,
            OWNER,
            BC_BOX,
            totalStates: 3,
            itemExtraData: """{"stuff":{"Data":"2","UniqueNumber":0,"UniqueSeries":0}}"""
        );

        Progress(box).Should().Be((0, 1, "0"));
        box.Logic.StuffData.GetSnapshot().StuffBitmask.Should().Be((int)StuffDataType.CrackableKey);

        await UseAsync(OWNER);
        await RunTimersAsync();

        ItemExists().Should().BeFalse();
        Extensions().Should().ContainSingle();
    }

    [Fact]
    public async Task Nobody_but_the_owner_can_open_an_owner_only_box()
    {
        _room.Enter(GUEST_AVATAR, 3, 4);
        var box = AddCrackable(BOX, 3, 3, OWNER, BC_BOX, totalStates: 3);

        await UseAsync(GUEST);
        await RunTimersAsync();

        Progress(box).Should().Be((0, 1, "0"));
        ItemExists().Should().BeTrue();
        Extensions().Should().BeEmpty();
    }

    [Fact]
    public async Task A_use_from_across_the_room_walks_over_and_is_not_a_hit()
    {
        var owner = _room.Enter(OWNER_AVATAR, 8, 8);
        var box = AddCrackable(BOX, 3, 3, OWNER, BC_BOX, totalStates: 3);

        await UseAsync(OWNER);

        Progress(box).Should().Be((0, 1, "0"));
        owner.IsWalking.Should().BeTrue();
        var (goalX, goalY) = _room.Map.GetTileXY(owner.GoalTileId);
        FloorFootprint.Of(box).DistanceTo(goalX, goalY).Should().Be(1);
        (goalX, goalY).Should().Be((4, 4), "the free tile beside it nearest to the avatar");
    }

    [Fact]
    public async Task An_egg_takes_everyones_hits_moves_through_its_states_and_counts_achievements()
    {
        _room.Enter(OWNER_AVATAR, 3, 4);
        _room.Enter(GUEST_AVATAR, 4, 3);
        var egg = AddCrackable(BOX, 3, 3, OWNER, EGG, totalStates: 5);

        var states = new List<string>();

        foreach (var player in new[] { GUEST, OWNER, GUEST })
        {
            await UseAsync(player);
            states.Add(Progress(egg).State);
        }

        // Untouched it draws state 0; each hit cracks it a little more (1-3), and state 4 - the
        // opening - is the cracking hit's alone.
        states.Should().Equal("1", "2", "3");
        Progress(egg).Should().Be((3, 4, "3"));
        ItemExists().Should().BeTrue();

        await UseAsync(GUEST);
        Progress(egg).Should().Be((4, 4, "4"));

        await RunTimersAsync();

        ItemExists().Should().BeFalse();
        // What it holds goes to the egg's owner, whoever cracked it.
        Extensions().Should().ContainSingle().Which.Player.Should().Be(OWNER);
        Facts(AchievementSources.CRACKABLE_HIT)
            .Should()
            .Equal(
                (GUEST, "eggcracker", 1L),
                (OWNER, "eggcracker", 1L),
                (GUEST, "eggcracker", 1L),
                (GUEST, "eggcracker", 1L)
            );
        Facts(AchievementSources.CRACKABLE_CRACKED).Should().Equal((GUEST, "eggmaster", 1L));
    }

    [Fact]
    public async Task A_crackable_needing_an_effect_takes_no_hit_without_it()
    {
        var owner = _room.Enter(OWNER_AVATAR, 3, 4);
        var plant = AddCrackable(
            BOX,
            3,
            3,
            OWNER,
            """{"crackable":{"rewardSet":"easter17_2","target":1,"hitBy":"Owner","requiredEffectId":192,"subscription":"HabboClub","subscriptionDays":1}}""",
            totalStates: 3
        );

        await UseAsync(OWNER);
        Progress(plant).Should().Be((0, 1, "0"));

        owner.SetEffect(192);
        await UseAsync(OWNER);
        Progress(plant).Should().Be((1, 1, "2"));
    }

    [Fact]
    public async Task A_crackable_holding_nothing_the_hotel_lists_takes_hits_but_never_cracks()
    {
        _room.Enter(OWNER_AVATAR, 3, 4);
        var bag = AddCrackable(
            BOX,
            3,
            3,
            OWNER,
            """{"crackable":{"rewardSet":"bonusrares16_1","target":2,"hitBy":"Anyone"}}""",
            totalStates: 3
        );

        await UseAsync(OWNER);
        await UseAsync(OWNER);
        await RunTimersAsync();

        Progress(bag).Should().Be((1, 2, "1"));
        ItemExists().Should().BeTrue();
    }

    [Fact]
    public async Task A_public_crackable_gives_its_furni_to_whoever_cracked_it()
    {
        _room.Enter(GUEST_AVATAR, 3, 4);
        _room.Harness.Fakes.Handlers["TryGetDefinitionByName"] = call =>
            call.Args[0] is "easter_r17_prize"
                ? Definition(777, "easter_r17_prize", 2, null)
                : null;
        AddCrackable(
            BOX,
            3,
            3,
            OWNER,
            """{"crackable":{"rewardSet":"easter17_6","target":1,"hitBy":"Anyone","rewardTo":"Cracker","rewards":[{"furni":"easter_r17_prize","weight":1}]}}""",
            totalStates: 3
        );

        await UseAsync(GUEST);
        await RunTimersAsync();

        ItemExists().Should().BeFalse();
        _room
            .Harness.Fakes.Log.Of("GrantFurnitureAsync")
            .Should()
            .ContainSingle()
            .Which.Should()
            .Match<FakeCall>(x => Equals(x.Key, (long)GUEST) || Equals(x.Key, GUEST));
        _room.Harness.Fakes.Log.Of("GrantFurnitureAsync").Single().Args[0].Should().Be(777);
    }

    [Fact]
    public async Task Hits_after_the_cracking_one_count_for_nothing_while_it_opens()
    {
        _room.Enter(OWNER_AVATAR, 3, 4);
        var box = AddCrackable(BOX, 3, 3, OWNER, BC_BOX, totalStates: 3);

        await UseAsync(OWNER);
        await UseAsync(OWNER);
        await UseAsync(OWNER);
        await RunTimersAsync();

        Extensions().Should().ContainSingle();
        box.Should().NotBeNull();
    }

    private async Task UseAsync(int playerId) =>
        await _room.Harness.Room.UseItemByIdAsync(
            ActionContext.CreateForPlayer(playerId, 1),
            BOX,
            Ct
        );

    private Task RunTimersAsync() =>
        _room.Harness.Module<RoomTimerSystem>().ProcessTimersAsync(long.MaxValue / 2, Ct);

    private bool ItemExists() =>
        ((IDictionary)RoomHarness.GetMember(_room.Harness.State, "ItemsById")!).Contains(
            (RoomObjectId)BOX
        );

    private static (int Hits, int Target, string State) Progress(IRoomFloorItem item)
    {
        var data = (ICrackableStuffData)item.Logic.StuffData;

        return (data.Hits, data.Target, data.GetLegacyString());
    }

    private List<(int Player, SubscriptionType Type, int Days)> Extensions() =>
        [
            .. _room
                .Harness.Fakes.Log.Of("ExtendAsync")
                .Select(x =>
                    (Convert.ToInt32(x.Key), (SubscriptionType)x.Args[0]!, (int)x.Args[1]!)
                ),
        ];

    private List<(int Player, string Value, long Amount)> Facts(string source) =>
        [
            .. _room
                .Harness.Fakes.Log.Of("Record")
                .Select(x =>
                    (Player: ((PlayerId)x.Args[1]!).Value, Fact: (AchievementFact)x.Args[2]!)
                )
                .Where(x => x.Fact.Source == source)
                .Select(x => (x.Player, x.Fact.Value, x.Fact.Amount)),
        ];

    private static FurnitureDefinitionSnapshot Definition(
        int id,
        string name,
        int totalStates,
        string? extraData
    ) =>
        new()
        {
            Id = id,
            SpriteId = id,
            Name = name,
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            LogicName = "crackable",
            TotalStates = totalStates,
            Width = 1,
            Length = 1,
            StackHeight = Altitude.FromInt(120),
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
            UsagePolicy = FurnitureUsageType.Nobody,
            ExtraData = extraData,
        };

    private IRoomFloorItem AddCrackable(
        int id,
        int x,
        int y,
        int ownerId,
        string definitionExtraData,
        int totalStates,
        string? itemExtraData = null
    )
    {
        var item = new RoomFloorItem
        {
            ObjectId = id,
            OwnerId = ownerId,
            OwnerName = "owner",
            Definition = Definition(1000 + id, "crackable_box", totalStates, definitionExtraData),
        };

        item.SetExtraData(itemExtraData);
        item.SetPosition(x, y);
        item.SetPositionZ(Altitude.Zero);
        item.SetRotation(Rotation.North);
        item.SetLogic(
            new FurnitureCrackableLogic(
                _room.StuffData,
                new RoomFloorItemContext(_room.Harness.Room, item)
            )
        );

        ((IDictionary)RoomHarness.GetMember(_room.Harness.State, "ItemsById")!).Add(
            (RoomObjectId)id,
            item
        );
        typeof(RoomMapModule)
            .GetMethod(
                "AddItem",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                [typeof(IRoomItem)]
            )!
            .Invoke(_room.Map, [item]);

        return item;
    }
}
