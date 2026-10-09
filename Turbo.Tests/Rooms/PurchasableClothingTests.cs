using FluentAssertions;
using Orleans;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Action;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Outgoing.Inventory.Clothing;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Clothing furni ("Use &amp; Bind Clothing", <c>CustomizeAvatarWithFurniMessageComposer</c>): its
/// owner gets the figure sets its customparams list, it is bound to them and used up, and the
/// client is told the sets and the bound furni (<c>FigureSetIdsMessage</c>), now and at every login.
/// </summary>
public class PurchasableClothingTests
{
    private const int ITEM = 7;
    private const int OWNER = 1;
    private const int GUEST = 5;
    private const string NAME = "clothing_nft26_trashcan";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_owner_binds_its_sets_and_the_furni_is_gone()
    {
        var room = CreateRoomWith("6621,6622");
        AddPlayer(room, OWNER);

        var done = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            ITEM,
            new BindClothingInteraction(),
            Ct
        );

        done.Should().BeTrue();
        var bind = room
            .Fakes.Log.Of(nameof(IPlayerClothingService.BindFurnitureAsync))
            .Should()
            .ContainSingle()
            .Subject;
        bind.Args[0].Should().Be((PlayerId)OWNER);
        bind.Args[1].Should().Be(1000 + ITEM, "the furni's definition is what is bound");
        ((IEnumerable<int>)bind.Args[2]!).Should().Equal(6621, 6622);
        room.ItemsById.Contains((RoomObjectId)ITEM).Should().BeFalse();
    }

    [Fact]
    public async Task Use_and_bind_asks_the_room_to_bind_the_furni_the_client_names()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("CustomizeAvatarWithFurniMessageEvent"),
            PacketHarness.Payload(w => w.Int(ITEM)),
            playerId: OWNER,
            roomId: 1
        );

        var interact = harness
            .Fakes.Log.Of("InteractWithItemAsync")
            .Should()
            .ContainSingle()
            .Subject;
        interact.Args[1].Should().Be((RoomObjectId)ITEM);
        interact.Args[2].Should().BeOfType<BindClothingInteraction>();
    }

    [Fact]
    public async Task Someone_else_cannot_bind_it()
    {
        var room = CreateRoomWith("6621,6622");
        AddPlayer(room, GUEST);

        var done = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(GUEST, 1),
            ITEM,
            new BindClothingInteraction(),
            Ct
        );

        done.Should().BeFalse();
        room.Fakes.Log.Of(nameof(IPlayerClothingService.BindFurnitureAsync)).Should().BeEmpty();
        room.ItemsById.Contains((RoomObjectId)ITEM).Should().BeTrue();
    }

    [Fact]
    public async Task A_furni_that_lists_no_sets_is_kept()
    {
        var room = CreateRoomWith(null);
        AddPlayer(room, OWNER);

        var done = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            ITEM,
            new BindClothingInteraction(),
            Ct
        );

        done.Should().BeFalse();
        room.Fakes.Log.Of(nameof(IPlayerClothingService.BindFurnitureAsync)).Should().BeEmpty();
        room.ItemsById.Contains((RoomObjectId)ITEM).Should().BeTrue();
    }

    [Fact]
    public async Task Binding_keeps_the_sets_and_the_furni_name_and_tells_the_player()
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
            new FurnitureDefinitionEntity
            {
                Id = 19285,
                SpriteId = 19285,
                Name = NAME,
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.FigurePurchasableSet,
                Logic = "default_floor",
                Width = 1,
                Length = 1,
                StackHeight = 1,
                CanStack = false,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = false,
                CanTrade = false,
                CanGroup = false,
                CanSell = false,
                CustomParams = "6621,6622",
            }
        );
        db.Insert(
            new PlayerFigureSetEntity
            {
                Id = 1,
                PlayerEntityId = OWNER,
                SetId = 6621,
            }
        );
        var fakes = new Fakes();
        var clothing = CreateClothingService(db, fakes);

        await clothing.BindFurnitureAsync(OWNER, 19285, [6621, 6622], Ct);
        await clothing.BindFurnitureAsync(OWNER, 19285, [6621, 6622], Ct);

        (await clothing.GetOwnedAsync(OWNER, Ct)).Should().BeEquivalentTo([6621, 6622]);
        (await clothing.GetBoundFurnitureNamesAsync(OWNER, Ct)).Should().Equal(NAME);
        var told = fakes
            .Log.Of("TrySendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<FigureSetIdsEventMessageComposer>()
            .ToList();
        told.Should().HaveCount(2, "the client waits for this answer to put the clothes on");
        told[^1].FigureSetIds.Should().Equal(6621, 6622);
        told[^1].BoundFurnitureNames.Should().Equal(NAME);
    }

    [Fact]
    public void Clothing_furni_get_the_clothing_logic_whatever_their_logic_column_says()
    {
        var room = new LiveRoomHarness();
        var item = new RoomHarness().CreateFloorItem(
            ITEM,
            2,
            2,
            Altitude.Zero,
            name: NAME,
            category: FurnitureCategory.FigurePurchasableSet,
            customParams: "6621,6622"
        );

        room.LogicProvider.CreateLogicInstance(
                "default_floor",
                new RoomFloorItemContext(room.Room, item)
            )
            .Should()
            .BeOfType<FurniturePurchasableClothingLogic>();
    }

    private static IPlayerClothingService CreateClothingService(SqliteDb db, Fakes fakes)
    {
        var type = typeof(Turbo.Players.PlayerModule).Assembly.GetType(
            "Turbo.Players.Figures.PlayerClothingService"
        )!;

        return (IPlayerClothingService)
            Activator.CreateInstance(type, db, fakes.Create<IGrainFactory>())!;
    }

    private static RoomHarness CreateRoomWith(string? customParams)
    {
        var room = new RoomHarness();

        // The room tells everyone in it that the furni is gone.
        var stream = typeof(RoomGrain).GetField(
            "_roomOutbound",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        )!;
        stream.SetValue(room.Room, room.Fakes.Create(stream.FieldType, "room-stream"));

        var clothing = room.Fakes.Create<IPlayerClothingService>();

        room.AddToRoom(
            room.CreateFloorItem(
                ITEM,
                2,
                2,
                Altitude.Zero,
                name: NAME,
                logic: FurniturePurchasableClothingLogic.LOGIC_NAME,
                createLogic: (stuffDataFactory, ctx) =>
                    new FurniturePurchasableClothingLogic(stuffDataFactory, ctx, clothing),
                category: FurnitureCategory.FigurePurchasableSet,
                customParams: customParams
            )
        );

        return room;
    }

    private static void AddPlayer(RoomHarness room, int playerId)
    {
        var player = room.Fakes.Create<IRoomPlayer>(playerId);

        room.Fakes.Handlers["get_PlayerId"] = call =>
            call.Interface == typeof(IRoomPlayer) && call.Key is int id
                ? (PlayerId)id
                : Fakes.NotHandled;

        (
            (IDictionary<PlayerId, RoomObjectId>)
                RoomHarness.GetMember(room.State, "AvatarsByPlayerId")!
        )[playerId] = playerId;
        (
            (IDictionary<RoomObjectId, IRoomAvatar>)
                RoomHarness.GetMember(room.State, "AvatarsByObjectId")!
        )[playerId] = player;
    }
}
