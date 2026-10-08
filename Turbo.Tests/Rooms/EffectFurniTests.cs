using System.Collections;
using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Furni that put avatar effects on, through the room's use and walk paths as the client reaches
/// them; the effect is each definition's <c>customparams</c>, as Habbo's furni data gives it.
/// An effect box (<c>fxbox_fx116</c>, 116) is opened once by its owner and the effect is theirs
/// for good; the Habbo Gun Vender (182) serves whoever uses it; the Builders Club water block
/// (30) puts its effect on whoever stands on it; an advert tile (157) also lights up while
/// stood on.
/// </summary>
public sealed class EffectFurniTests
{
    private const int FURNI = 70;
    private const int OWNER_AVATAR = 1;
    private const int GUEST_AVATAR = 2;
    private const int OWNER = 100 + OWNER_AVATAR;
    private const int GUEST = 100 + GUEST_AVATAR;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(10, 10);
    private long _now;

    public EffectFurniTests()
    {
        RoomHarness.SetField(
            _room.Harness.Room,
            "_roomConfig",
            new RoomConfig { VendingDispenseMs = 0 }
        );
        var snapshot = RoomHarness.GetMember(_room.Harness.State, "RoomSnapshot")!;
        RoomHarness.SetMember(snapshot, "OwnerId", (PlayerId)OWNER);
        _room.Harness.Fakes.Handlers["get_RoomObject"] = call =>
            call.Interface == typeof(IRoomPlayerContext)
            && call.Key is int id
            && _room.Avatars.TryGetValue(id, out var avatar)
                ? avatar
                : Fakes.NotHandled;
    }

    [Fact]
    public async Task An_effect_box_opened_by_its_owner_is_gone_and_its_effect_is_theirs_for_good_and_worn()
    {
        _room.Enter(OWNER_AVATAR, 5, 5);
        AddFurni(FURNI, 3, 3, "effect_box", "116", (s, c) => new FurnitureEffectBoxLogic(s, c));

        await UseAsync(OWNER, FURNI);

        Grants().Should().Equal((OWNER, 116, true));
        ItemExists(FURNI).Should().BeFalse();
        _room.Harness.Fakes.Log.Of("SelectEffectAsync").Select(x => x.Args[0]).Should().Equal(116);
    }

    [Fact]
    public async Task Nobody_but_its_owner_can_open_an_effect_box()
    {
        _room.Enter(GUEST_AVATAR, 5, 5);
        AddFurni(FURNI, 3, 3, "effect_box", "116", (s, c) => new FurnitureEffectBoxLogic(s, c));

        await UseAsync(GUEST, FURNI);

        Grants().Should().BeEmpty();
        ItemExists(FURNI).Should().BeTrue();
    }

    [Fact]
    public async Task An_effect_box_whose_effect_the_owner_has_for_good_stays_shut()
    {
        _room.Harness.Fakes.Handlers["GiveEffectAsync"] = _ =>
            Task.FromResult(EffectGrantResult.AlreadyPermanent);
        _room.Enter(OWNER_AVATAR, 5, 5);
        AddFurni(FURNI, 3, 3, "effect_box", "116", (s, c) => new FurnitureEffectBoxLogic(s, c));

        await UseAsync(OWNER, FURNI);

        ItemExists(FURNI).Should().BeTrue();
        _room.Harness.Fakes.Log.Of("SelectEffectAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task The_gun_vender_puts_its_effect_on_whoever_uses_it_from_beside_it()
    {
        var guest = _room.Enter(GUEST_AVATAR, 3, 4);
        AddFurni(
            FURNI,
            3,
            3,
            "effect_provider",
            "182",
            (s, c) => new FurnitureEffectProviderLogic(s, c),
            totalStates: 2,
            usage: FurnitureUsageType.Everybody
        );

        await UseAsync(GUEST, FURNI);
        await WalkUntilStoppedAsync(guest);

        guest.EffectId.Should().Be(182);
        Grants().Should().BeEmpty("the hotel's effect is not added to the player's own");
    }

    [Fact]
    public async Task Crossing_a_water_area_wears_its_effect_all_the_way_and_takes_it_off_after()
    {
        var avatar = _room.Enter(OWNER_AVATAR, 1, 3);
        AddArea(FURNI, 2, 3);
        AddArea(FURNI + 1, 3, 3);

        await WalkToAsync(
            avatar,
            4,
            3,
            onEachStep: () =>
            {
                var x =
                    avatar.NextTileId >= 0 ? _room.Map.GetTileXY(avatar.NextTileId).x : avatar.X;

                if (x is 2 or 3)
                    avatar.EffectId.Should().Be(30, $"standing on the water at x {x}");
            }
        );

        (avatar.X, avatar.Y).Should().Be((4, 3));
        avatar.EffectId.Should().Be(0);
        EffectMessages(avatar).Should().Equal([30, 0], "it is put on once and taken off once");
    }

    [Fact]
    public async Task An_avatar_that_put_on_another_effect_on_the_water_keeps_it_stepping_off()
    {
        var avatar = _room.Enter(OWNER_AVATAR, 1, 3);
        AddArea(FURNI, 2, 3);

        await WalkToAsync(avatar, 2, 3);
        avatar.SetEffect(7);
        await WalkToAsync(avatar, 3, 3);

        avatar.EffectId.Should().Be(7);
    }

    [Fact]
    public async Task An_effect_tile_gives_its_effect_to_keep_and_lights_up_while_it_is_stood_on()
    {
        var avatar = _room.Enter(OWNER_AVATAR, 1, 3);
        var tile = AddTile(FURNI, 2, 3, "157");

        await WalkToAsync(avatar, 2, 3);

        tile.Logic.GetState().Should().Be(1);
        avatar.EffectId.Should().Be(157);

        await WalkToAsync(avatar, 3, 3);

        tile.Logic.GetState().Should().Be(0);
        avatar.EffectId.Should().Be(157, "the new-user room's tiles hand out effects to keep");
    }

    [Fact]
    public async Task The_new_user_rooms_remove_tile_takes_the_effect_off()
    {
        var avatar = _room.Enter(OWNER_AVATAR, 1, 3);
        AddTile(FURNI, 2, 3, "2,0");
        AddTile(FURNI + 1, 4, 3, "0,0");

        await WalkToAsync(avatar, 3, 3);

        avatar.EffectId.Should().Be(2);

        await WalkToAsync(avatar, 5, 3);

        avatar.EffectId.Should().Be(0);
    }

    private Task UseAsync(int playerId, int objectId) =>
        _room.Harness.Room.UseItemByIdAsync(
            ActionContext.CreateForPlayer(playerId, 1),
            objectId,
            Ct
        );

    /// <summary>Walks the avatar there tick by tick, running the room's timers between ticks.</summary>
    private async Task WalkToAsync(RoomPlayerAvatar avatar, int x, int y, Action? onEachStep = null)
    {
        (
            await _room.Harness.Room.WalkAvatarToAsync(
                ActionContext.CreateForPlayer(100 + (int)avatar.ObjectId, 1),
                x,
                y,
                Ct
            )
        )
            .Should()
            .BeTrue();

        for (var tick = 0; tick < 30 && avatar.IsWalking; tick++)
        {
            _now += 10_000;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
            await RunTimersAsync();
            onEachStep?.Invoke();
        }

        await RunTimersAsync();
    }

    private async Task WalkUntilStoppedAsync(RoomPlayerAvatar avatar)
    {
        for (var tick = 0; tick < 30 && avatar.IsWalking; tick++)
        {
            _now += 10_000;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
        }

        await RunTimersAsync();
    }

    // One room tick ahead: far enough for the furni's own timers, short of any expiry.
    private Task RunTimersAsync() =>
        _room
            .Harness.Module<RoomTimerSystem>()
            .ProcessTimersAsync(
                (long)(
                    System.Diagnostics.Stopwatch.GetTimestamp()
                    * 1000.0
                    / System.Diagnostics.Stopwatch.Frequency
                ) + 500,
                Ct
            );

    private List<(int Player, int EffectId, bool Permanent)> Grants() =>
        [
            .. _room
                .Harness.Fakes.Log.Of("GiveEffectAsync")
                .Select(x => (Convert.ToInt32(x.Key), (int)x.Args[0]!, (bool)x.Args[3]!)),
        ];

    private List<int> EffectMessages(RoomPlayerAvatar avatar) =>
        [
            .. _room
                .Harness.Fakes.Log.Of("OnNextAsync")
                .Select(x => x.Args[0])
                .OfType<Turbo.Primitives.Rooms.Snapshots.RoomOutboundSnapshot>()
                .SelectMany(x => x.Composers)
                .OfType<Turbo.Primitives.Messages.Outgoing.Room.Action.AvatarEffectMessageComposer>()
                .Where(x => x.ObjectId == avatar.ObjectId)
                .Select(x => x.EffectId),
        ];

    private bool ItemExists(int objectId) =>
        ((IDictionary)RoomHarness.GetMember(_room.Harness.State, "ItemsById")!).Contains(
            (RoomObjectId)objectId
        );

    private IRoomFloorItem AddTile(int objectId, int x, int y, string customParams) =>
        AddFurni(
            objectId,
            x,
            y,
            "effect_tile",
            customParams,
            (s, c) => new FurnitureEffectTileLogic(s, c),
            totalStates: 2,
            canWalk: true
        );

    private IRoomFloorItem AddArea(int objectId, int x, int y) =>
        AddFurni(
            objectId,
            x,
            y,
            "effect_area",
            "30",
            (s, c) => new FurnitureEffectAreaLogic(s, c),
            canWalk: true
        );

    private IRoomFloorItem AddFurni(
        int objectId,
        int x,
        int y,
        string logicName,
        string customParams,
        Func<IStuffDataFactory, IRoomFloorItemContext, FurnitureFloorLogic> logic,
        int totalStates = 1,
        FurnitureUsageType usage = FurnitureUsageType.Controller,
        bool canWalk = false
    )
    {
        var item = new RoomFloorItem
        {
            ObjectId = objectId,
            OwnerId = OWNER,
            OwnerName = "owner",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 1000 + objectId,
                SpriteId = 1000 + objectId,
                Name = logicName,
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = logicName,
                TotalStates = totalStates,
                Width = 1,
                Length = 1,
                StackHeight = Altitude.FromInt(canWalk ? 0 : 100),
                CanStack = canWalk,
                CanWalk = canWalk,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = usage,
                CustomParams = customParams,
                ExtraData = null,
            },
        };

        item.SetExtraData(null);
        item.SetPosition(x, y);
        item.SetPositionZ(Altitude.Zero);
        item.SetRotation(Rotation.North);
        item.SetLogic(logic(_room.StuffData, new RoomFloorItemContext(_room.Harness.Room, item)));

        ((IDictionary)RoomHarness.GetMember(_room.Harness.State, "ItemsById")!).Add(
            (RoomObjectId)objectId,
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
