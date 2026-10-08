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
using Turbo.Primitives.Players.Wallet;
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
/// Crackable furni, driven through the room's use and walk-on paths as the client reaches them:
/// the Habbo Club and Builders Club boxes, multi-hit eggs anyone may tap, effect-dependent plants,
/// piñatas hit by walking under them with the stick, bonus bags, and the boxes left in their empty
/// opening frame before the logic existed (the "ghost blocks"). Who may hit is the definition's
/// usage policy, as Sulake's furni data gives it.
/// </summary>
public sealed class CrackableFurniTests
{
    private const int BOX = 50;
    private const int OWNER_AVATAR = 1;
    private const int GUEST_AVATAR = 2;
    private const int OWNER = 100 + OWNER_AVATAR;
    private const int GUEST = 100 + GUEST_AVATAR;
    private const int PINATA_STICK = 158;

    private const string BC_BOX =
        """{"crackable":{"rewardSet":"bcgift_1","target":1,"rewards":[{"subscription":"BuildersClub","subscriptionDays":14}]}}""";

    private const string EGG =
        """{"crackable":{"rewardSet":"egg_p1","target":4,"incrementalHitAchievement":"EggCracker","finalHitAchievement":"EggMaster","rewards":[{"credits":1}]}}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(10, 10);

    public CrackableFurniTests()
    {
        // The owner of the furni owns the room, so holds its rights.
        var snapshot = RoomHarness.GetMember(_room.Harness.State, "RoomSnapshot")!;
        RoomHarness.SetMember(snapshot, "OwnerId", (PlayerId)OWNER);
        // A walk-on hands the furni the walking avatar's context.
        _room.Harness.Fakes.Handlers["get_RoomObject"] = call =>
            call.Interface == typeof(IRoomPlayerContext)
            && call.Key is int id
            && _room.Avatars.TryGetValue(id, out var avatar)
                ? avatar
                : Fakes.NotHandled;
    }

    [Fact]
    public async Task A_bc_box_opens_on_its_owners_hit_and_gives_the_membership_once_it_is_gone()
    {
        _room.Enter(OWNER_AVATAR, 3, 4);
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
    }

    [Fact]
    public async Task A_box_toggled_to_its_empty_opening_frame_before_comes_back_whole()
    {
        _room.Enter(OWNER_AVATAR, 3, 4);

        // How the reported BC boxes were stored: legacy stuff data in state 2, the asset's empty
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
    public async Task A_visitor_without_rights_cannot_hit_a_box_that_is_not_everyones()
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
        (goalX, goalY).Should().Be((4, 4), "the free tile beside it nearest to the avatar");
    }

    [Fact]
    public async Task An_egg_everyone_may_use_takes_everyones_hits_and_counts_their_achievements()
    {
        _room.Enter(OWNER_AVATAR, 3, 4);
        _room.Enter(GUEST_AVATAR, 4, 3);
        var egg = AddCrackable(
            BOX,
            3,
            3,
            OWNER,
            EGG,
            totalStates: 5,
            usage: FurnitureUsageType.Everybody
        );

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
        Credits().Should().Equal((OWNER, 1L));
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
            """{"crackable":{"rewardSet":"easter17_2","target":1,"requiredEffectId":192,"rewards":[{"credits":1}]}}""",
            totalStates: 3
        );

        await UseAsync(OWNER);
        Progress(plant).Should().Be((0, 1, "0"));

        owner.SetEffect(192);
        await UseAsync(OWNER);
        Progress(plant).Should().Be((1, 1, "2"));
    }

    [Fact]
    public async Task A_pinata_is_hit_by_walking_under_it_with_the_stick_and_not_by_a_double_click()
    {
        var walker = _room.Enter(GUEST_AVATAR, 3, 4);
        var pinata = AddCrackable(
            BOX,
            3,
            3,
            OWNER,
            """{"crackable":{"rewardSet":"pinata1","target":2,"hitOn":"Walk","requiredEffectId":158,"incrementalHitAchievement":"PinataWhacker","finalHitAchievement":"PinataBreaker","rewards":[{"credits":1}]}}""",
            totalStates: 9,
            usage: FurnitureUsageType.Everybody,
            canWalk: true
        );

        await UseAsync(GUEST);
        await WalkOnAsync(walker);
        Progress(pinata)
            .Should()
            .Be((0, 2, "0"), "neither a use nor a walk without the stick hits it");

        walker.SetEffect(PINATA_STICK);
        await WalkOnAsync(walker);
        Progress(pinata).Should().Be((1, 2, "4"));
        await WalkOnAsync(walker);
        Progress(pinata).Should().Be((2, 2, "8"));

        await RunTimersAsync();

        ItemExists().Should().BeFalse();
        Facts(AchievementSources.CRACKABLE_HIT)
            .Should()
            .Equal((GUEST, "pinatawhacker", 1L), (GUEST, "pinatawhacker", 1L));
        Facts(AchievementSources.CRACKABLE_CRACKED).Should().Equal((GUEST, "pinatabreaker", 1L));
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
            """{"crackable":{"rewardSet":"bonusrares16_1","target":2}}""",
            totalStates: 3
        );

        await UseAsync(OWNER);
        await UseAsync(OWNER);
        await RunTimersAsync();

        Progress(bag).Should().Be((1, 2, "1"));
        ItemExists().Should().BeTrue();
    }

    [Fact]
    public async Task A_bonus_bag_can_give_credits_or_a_membership_instead_of_a_furni()
    {
        _room.Enter(OWNER_AVATAR, 3, 4);
        AddCrackable(
            BOX,
            3,
            3,
            OWNER,
            """{"crackable":{"rewardSet":"bonusrares16_1","target":1,"rewards":[{"credits":5,"weight":1},{"furni":"","weight":5}]}}""",
            totalStates: 3
        );

        await UseAsync(OWNER);
        await RunTimersAsync();

        // The furni entry names nothing, so it is never drawn: the bag gives its 5 credits.
        ItemExists().Should().BeFalse();
        Credits().Should().Equal((OWNER, 5L));
    }

    [Fact]
    public async Task A_public_crackable_gives_its_furni_to_whoever_cracked_it()
    {
        _room.Enter(GUEST_AVATAR, 3, 4);
        _room.Harness.Fakes.Handlers["TryGetDefinitionByName"] = call =>
            call.Args[0] is "easter_r17_prize"
                ? Definition(777, "easter_r17_prize", 2, null, FurnitureUsageType.Controller)
                : null;
        AddCrackable(
            BOX,
            3,
            3,
            OWNER,
            """{"crackable":{"rewardSet":"easter17_6","target":1,"rewardTo":"Cracker","rewards":[{"furni":"easter_r17_prize"}]}}""",
            totalStates: 3,
            usage: FurnitureUsageType.Everybody
        );

        await UseAsync(GUEST);
        await RunTimersAsync();

        ItemExists().Should().BeFalse();
        var grant = _room
            .Harness.Fakes.Log.Of("GrantFurnitureAsync")
            .Should()
            .ContainSingle()
            .Subject;
        Convert.ToInt32(grant.Key).Should().Be(GUEST);
        grant.Args[0].Should().Be(777);
    }

    [Fact]
    public async Task A_draw_can_be_a_set_of_furni_all_given_together()
    {
        _room.Enter(OWNER_AVATAR, 3, 4);
        _room.Harness.Fakes.Handlers["TryGetDefinitionByName"] = call =>
            call.Args[0] switch
            {
                "metal_ingot" => Definition(901, "metal_ingot", 1, null, FurnitureUsageType.Nobody),
                "potion_magic" => Definition(
                    902,
                    "potion_magic",
                    1,
                    null,
                    FurnitureUsageType.Nobody
                ),
                _ => null,
            };
        AddCrackable(
            BOX,
            3,
            3,
            OWNER,
            """{"crackable":{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"metal_ingot","also":["metal_ingot","metal_ingot","potion_magic"]}]}}""",
            totalStates: 3
        );

        await UseAsync(OWNER);
        await RunTimersAsync();

        // A supply chest's "Metal Ingot (x3), Potion of Magic".
        _room
            .Harness.Fakes.Log.Of("GrantFurnitureAsync")
            .Select(x => (int)x.Args[0]!)
            .Should()
            .Equal(901, 901, 901, 902);
    }

    [Fact]
    public async Task A_crackable_with_several_draws_gives_one_reward_for_each()
    {
        _room.Enter(OWNER_AVATAR, 3, 4);
        _room.Harness.Fakes.Handlers["TryGetDefinitionByName"] = call =>
            call.Args[0] switch
            {
                "coral" => Definition(901, "coral", 1, null, FurnitureUsageType.Nobody),
                "trident" => Definition(902, "trident", 1, null, FurnitureUsageType.Nobody),
                _ => null,
            };
        AddCrackable(
            BOX,
            3,
            3,
            OWNER,
            """{"crackable":{"target":1,"rewardPlacement":"Inventory","rewards":[{"credits":99}],"draws":[{"count":3,"rewards":[{"furni":"coral"}]},{"rewards":[{"furni":"trident"}]}]}}""",
            totalStates: 3
        );

        await UseAsync(OWNER);
        await RunTimersAsync();

        // A Coral Kingdom Chest: three commons, then one more from its own pool.
        _room
            .Harness.Fakes.Log.Of("GrantFurnitureAsync")
            .Select(x => (int)x.Args[0]!)
            .Should()
            .Equal(901, 901, 901, 902);
        Credits().Should().BeEmpty("the draws replace the single draw from rewards");
    }

    [Fact]
    public async Task A_crackable_taking_either_of_two_effects_takes_a_hit_with_either()
    {
        var owner = _room.Enter(OWNER_AVATAR, 3, 4);
        var rock = AddCrackable(
            BOX,
            3,
            3,
            OWNER,
            """{"crackable":{"target":3,"requiredEffectIds":[182,183],"rewards":[{"credits":1}]}}""",
            totalStates: 3
        );

        owner.SetEffect(186);
        await UseAsync(OWNER);
        owner.SetEffect(182);
        await UseAsync(OWNER);
        owner.SetEffect(183);
        await UseAsync(OWNER);

        Progress(rock).Hits.Should().Be(2);
    }

    [Fact]
    public async Task Hits_after_the_cracking_one_count_for_nothing_while_it_opens()
    {
        _room.Enter(OWNER_AVATAR, 3, 4);
        AddCrackable(BOX, 3, 3, OWNER, BC_BOX, totalStates: 3);

        await UseAsync(OWNER);
        await UseAsync(OWNER);
        await UseAsync(OWNER);
        await RunTimersAsync();

        Extensions().Should().ContainSingle();
    }

    private async Task UseAsync(int playerId) =>
        await _room.Harness.Room.UseItemByIdAsync(
            ActionContext.CreateForPlayer(playerId, 1),
            BOX,
            Ct
        );

    private Task WalkOnAsync(IRoomAvatar avatar)
    {
        var item = _room.FloorItem(BOX);

        return _room
            .Harness.Module<RoomAvatarModule>()
            .NotifyWalkOnAsync(avatar, _room.Map.ToIdx(item.X, item.Y), Ct);
    }

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

    private List<(int Player, long Amount)> Credits() =>
        [
            .. _room
                .Harness.Fakes.Log.Of("CreditAsync")
                .Where(x => x.Args[0] is CurrencyKind kind && kind == CurrencyKind.Credits)
                .Select(x => (Convert.ToInt32(x.Key), Convert.ToInt64(x.Args[1]))),
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
        string? extraData,
        FurnitureUsageType usage,
        bool canWalk = false
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
            CanWalk = canWalk,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
            UsagePolicy = usage,
            ExtraData = extraData,
        };

    private IRoomFloorItem AddCrackable(
        int id,
        int x,
        int y,
        int ownerId,
        string definitionExtraData,
        int totalStates,
        string? itemExtraData = null,
        FurnitureUsageType usage = FurnitureUsageType.Controller,
        bool canWalk = false
    )
    {
        var item = new RoomFloorItem
        {
            ObjectId = id,
            OwnerId = ownerId,
            OwnerName = "owner",
            Definition = Definition(
                1000 + id,
                "crackable_box",
                totalStates,
                definitionExtraData,
                usage,
                canWalk
            ),
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
