using System.Collections;
using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Quests;
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
/// A teleport between the two halves of a pair in one room, as the client sees it: the states each
/// half is set to, in order.
/// </summary>
public sealed class TeleportSequenceTests
{
    private const int TELE_A = 90;
    private const int TELE_B = 91;
    private const int OWNER_AVATAR = 1;
    private const int OWNER = 100 + OWNER_AVATAR;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(10, 10);
    private readonly List<string> _states = [];
    private long _now;

    public TeleportSequenceTests()
    {
        RoomHarness.SetField(
            _room.Harness.Room,
            "_roomConfig",
            new RoomConfig { TeleportStepMs = 1, AvatarTickMs = 1 }
        );
        _room.Harness.Fakes.Handlers["get_RoomObject"] = call =>
            call.Interface == typeof(IRoomPlayerContext)
            && call.Key is int id
            && _room.Avatars.TryGetValue(id, out var avatar)
                ? avatar
                : Fakes.NotHandled;
    }

    [Fact]
    public async Task Each_half_opens_once_in_a_teleport()
    {
        var avatar = _room.Enter(OWNER_AVATAR, 2, 4);
        var a = AddTele(TELE_A, 2, 3, partner: TELE_B);
        AddTele(TELE_B, 7, 7, partner: TELE_A);

        await a.Logic.OnUseAsync(ActionContext.CreateForPlayer(OWNER, 1), 0, Ct);

        for (var round = 0; round < 60; round++)
        {
            _now += 10_000;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
            await RunTimersAsync();
        }

        // The near half opens, takes them in, shuts and flashes; the far half flashes, opens to
        // let them out and shuts behind them: each opens once.
        _states.Should().Equal("A:1", "A:0", "A:2", "A:0", "B:2", "B:1", "B:0");
        // The teleport counts once for a reward track's teleport task.
        _room
            .Harness.Fakes.Log.Of("RecordActionAsync")
            .Select(x => (Convert.ToInt32(x.Key), x.Args[0]))
            .Should()
            .Equal((OWNER, RewardTrackActionTypes.TELEPORT));
    }

    [Fact]
    public async Task To_another_room_the_door_opens_once()
    {
        const int FAR_HALF = 999;
        var db = new InMemoryDb();

        await using (var ctx = await db.CreateDbContextAsync(Ct))
        {
            ctx.Furnitures.Add(
                new Turbo.Database.Entities.Furniture.FurnitureEntity
                {
                    Id = FAR_HALF,
                    PlayerEntityId = OWNER,
                    FurnitureDefinitionEntityId = 1,
                    RoomEntityId = 2,
                }
            );
            await ctx.SaveChangesAsync(Ct);
        }

        RoomHarness.SetField(_room.Harness.Room, "_dbCtxFactory", db);

        var avatar = _room.Enter(OWNER_AVATAR, 2, 4);
        var a = AddTele(TELE_A, 2, 3, partner: FAR_HALF);

        await a.Logic.OnUseAsync(ActionContext.CreateForPlayer(OWNER, 1), 0, Ct);

        var forwarded = false;

        for (var round = 0; round < 60; round++)
        {
            _now += 10_000;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
            await RunTimersAsync();

            // The presence takes them out of this room as soon as their client asks for the
            // other one, which it does on the forward.
            if (
                !forwarded
                && _room.Harness.Fakes.Log.Calls.Any(x => x.Method == "ForwardToRoomAsync")
            )
            {
                forwarded = true;
                await _room.Harness.Room.RemoveAvatarFromPlayerAsync(
                    ActionContext.CreateForPlayer(OWNER, 1),
                    OWNER,
                    Ct
                );
            }
        }

        // Opens, takes them in, shuts and flashes, and shuts again once they have gone.
        _states.Should().Equal("A:1", "A:0", "A:2", "A:0");
    }

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

    /// <summary>A teleporter that writes down each state it is set to.</summary>
    private sealed class Recording(
        IStuffDataFactory stuff,
        IRoomFloorItemContext ctx,
        List<string> states,
        string name
    ) : FurnitureTeleportLogic(stuff, ctx)
    {
        public override Task SetStateAsync(int state, bool refresh = true)
        {
            states.Add($"{name}:{state}");

            return base.SetStateAsync(state, refresh);
        }
    }

    private IRoomFloorItem AddTele(int objectId, int x, int y, int partner)
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
                Name = "door",
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = TeleportFurniture.LOGIC_NAME,
                TotalStates = 3,
                Width = 1,
                Length = 1,
                StackHeight = Altitude.FromInt(0),
                CanStack = false,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Everybody,
                CustomParams = "",
                ExtraData = null,
            },
        };

        item.SetExtraData(null);
        item.SetPosition(x, y);
        item.SetPositionZ(Altitude.Zero);
        item.SetRotation(Rotation.South);

        var logic = new Recording(
            _room.StuffData,
            new RoomFloorItemContext(_room.Harness.Room, item),
            _states,
            objectId == TELE_A ? "A" : "B"
        );

        item.SetLogic(logic);
        logic.LinkTo(partner);

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
