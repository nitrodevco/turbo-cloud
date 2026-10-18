using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Redeeming credit furni (the client's exchange dialog): its owner gets the value the furni's
/// class name carries, in each way Habbo writes it, and the furni is gone.
/// </summary>
public class CreditFurniRedeemTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const int ITEM = 7;
    private const int OWNER = 1;
    private const int GUEST = 5;

    [Theory]
    [InlineData("CF_10_coin_gold", 10)]
    [InlineData("CF_50_goldbar", 50)]
    [InlineData("CFC_500_goldbar", 500)]
    [InlineData("CFC_10_coin_bronze", 10)]
    [InlineData("CF_diamond_2500", 2500)]
    public async Task The_owner_gets_its_value_and_the_furni_is_gone(string name, int credits)
    {
        var room = CreateRoomWith(name);
        AddPlayer(room, OWNER);
        room.Fakes.Handlers[nameof(IPlayerWalletGrain.CreditAsync)] = _ => Task.FromResult(true);

        var done = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            ITEM,
            new RedeemCreditsInteraction(),
            Ct
        );

        done.Should().BeTrue();
        room.Fakes.Log.Of(nameof(IPlayerWalletGrain.CreditAsync))
            .Should()
            .ContainSingle(x => x.Args.Contains(credits));
        room.ItemsById.Contains((RoomObjectId)ITEM).Should().BeFalse();
    }

    // The wallet writes the credits at once; the furni's row was only queued for the next flush.
    // r36 rig (CF_5_coin_silver, agent4 room): the row went 2204 ms after the client saw the coin
    // go, and the server killed 374 ms after the redeem left the row with the credits kept, so
    // the coin came back to be redeemed again.
    [Fact]
    public async Task The_redeemed_furni_is_deleted_from_the_database_before_the_redeem_returns()
    {
        var room = CreateRoomWith("CF_5_coin_silver");
        AddPlayer(room, OWNER);
        room.Fakes.Handlers[nameof(IPlayerWalletGrain.CreditAsync)] = _ => Task.FromResult(true);

        await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            ITEM,
            new RedeemCreditsInteraction(),
            Ct
        );

        room.Fakes.Log.Of("WriteUseAsync")
            .Select(x => x.Args[1])
            .Should()
            .Equal((RoomObjectId)ITEM);
        room.Fakes.Log.Of("EnqueueDeletedItemAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task Someone_else_cannot_redeem_it()
    {
        var room = CreateRoomWith("CF_10_coin_gold");
        AddPlayer(room, GUEST);

        var done = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(GUEST, 1),
            ITEM,
            new RedeemCreditsInteraction(),
            Ct
        );

        done.Should().BeFalse();
        room.Fakes.Log.Of(nameof(IPlayerWalletGrain.CreditAsync)).Should().BeEmpty();
        room.ItemsById.Contains((RoomObjectId)ITEM).Should().BeTrue();
    }

    [Fact]
    public async Task A_furni_whose_name_carries_no_value_is_kept()
    {
        var room = CreateRoomWith("nft_emerald_eggmerald");
        AddPlayer(room, OWNER);

        var done = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            ITEM,
            new RedeemCreditsInteraction(),
            Ct
        );

        done.Should().BeFalse();
        room.Fakes.Log.Of(nameof(IPlayerWalletGrain.CreditAsync)).Should().BeEmpty();
        room.ItemsById.Contains((RoomObjectId)ITEM).Should().BeTrue();
    }

    // Hotel report 2026-10-14: a stack of money bags copied by Place Temporary Furni, which
    // carry the wired box owner's id, turned into credits; Builders Club furni carry the
    // borrower's.
    [Theory]
    [InlineData(FurniIdBands.TemporaryMin)]
    [InlineData(FurniIdBands.BuildersClubMin)]
    public async Task A_borrowed_or_temporary_furni_is_not_redeemed_by_its_owner(int id)
    {
        var room = CreateRoomWith("CF_20_moneybag", id);
        AddPlayer(room, OWNER);
        room.Fakes.Handlers[nameof(IPlayerWalletGrain.CreditAsync)] = _ => Task.FromResult(true);

        var done = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            id,
            new RedeemCreditsInteraction(),
            Ct
        );

        done.Should().BeFalse();
        room.Fakes.Log.Of(nameof(IPlayerWalletGrain.CreditAsync)).Should().BeEmpty();
        room.ItemsById.Contains((RoomObjectId)id).Should().BeTrue();
    }

    private static RoomHarness CreateRoomWith(string name, int id = ITEM)
    {
        var room = new RoomHarness();

        // The room tells everyone in it that the furni is gone.
        var stream = typeof(RoomGrain).GetField(
            "_roomOutbound",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        )!;
        stream.SetValue(room.Room, room.Fakes.Create(stream.FieldType, "room-stream"));

        room.AddToRoom(
            room.CreateFloorItem(
                id,
                2,
                2,
                Altitude.Zero,
                name: name,
                logic: "exchange",
                createLogic: (stuffDataFactory, ctx) =>
                    new FurnitureExchangeLogic(stuffDataFactory, ctx)
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
