using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Entities.Furniture;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room.Engine;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>
/// An item id a client sends is looked up in the sender's own inventory, never in the inventory
/// the item belongs to: a player who names another player's item (one a plugin let them see, or
/// one guessed) places nothing and trades nothing, and both inventories and the rows stay as they
/// were. Every client message that moves furni out of an inventory goes through the acting
/// player's own inventory grain; these pin the ones that hand an item to a room or a trade.
/// </summary>
public sealed class InventoryOwnershipTests : IDisposable
{
    private const int ACTOR = 8;
    private const int PARTNER = 9;
    private const int OWNER = 10;
    private const int ROOM = 5;

    private const int CHAIR = 1;
    private const int POSTER = 2;

    private const int ACTORS_CHAIR = 100;
    private const int ACTORS_POSTER = 101;
    private const int OWNERS_CHAIR = 200;
    private const int OWNERS_POSTER = 201;

    private readonly FurnitureWorld _world = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public InventoryOwnershipTests()
    {
        _world.AddPlayer(ACTOR, "actor");
        _world.AddPlayer(PARTNER, "partner");
        _world.AddPlayer(OWNER, "owner");
        _world.AddDefinition(CHAIR, "chair", "default_floor");
        _world.AddDefinition(POSTER, "poster", "default_wall", ProductType.Wall);
        _world.AddFurni(ACTORS_CHAIR, ACTOR, CHAIR, roomId: null);
        _world.AddFurni(ACTORS_POSTER, ACTOR, POSTER, roomId: null);
        _world.AddFurni(OWNERS_CHAIR, OWNER, CHAIR, roomId: null);
        _world.AddFurni(OWNERS_POSTER, OWNER, POSTER, roomId: null);
    }

    public void Dispose() => _world.Dispose();

    [Theory]
    [InlineData("{0} 3 3 0", nameof(IRoomGrain.PlaceFloorItemAsync), OWNERS_CHAIR, ACTORS_CHAIR)]
    [InlineData(
        "{0} :w=1,2 l=3,4 r",
        nameof(IRoomGrain.PlaceWallItemAsync),
        OWNERS_POSTER,
        ACTORS_POSTER
    )]
    public async Task Placing_another_players_item_reaches_no_room_and_leaves_it_theirs(
        string data,
        string placement,
        int othersItem,
        int ownItem
    )
    {
        var actor = await InventoryOfAsync(ACTOR);
        var owner = await InventoryOfAsync(OWNER);
        var before = _world.Rows();

        await PlaceAsync(string.Format(data, othersItem));
        await PlaceAsync(string.Format(data, -othersItem));

        Placed(placement).Should().BeEmpty("the actor's own inventory does not hold the item");
        (await owner.GetItemSnapshotAsync(othersItem, Ct)).Should().NotBeNull();
        (await actor.GetItemSnapshotAsync(othersItem, Ct)).Should().BeNull();
        _world.Rows().Should().BeEquivalentTo(before);

        // The same message naming the actor's own item does reach the room.
        await PlaceAsync(string.Format(data, ownItem));

        Placed(placement).Should().Equal(ownItem);
    }

    [Fact]
    public async Task Offering_another_players_item_in_a_trade_adds_nothing_and_moves_nothing()
    {
        await InventoryOfAsync(ACTOR);
        await InventoryOfAsync(PARTNER);
        var owner = await InventoryOfAsync(OWNER);
        var trades = NewTradeGrain();
        var actorCtx = ActionContext.CreateForPlayer(ACTOR, ROOM);
        var partnerCtx = ActionContext.CreateForPlayer(PARTNER, ROOM);

        (await trades.OpenAsync(actorCtx, 2, Ct)).Should().BeTrue();
        (await trades.AddItemsAsync(actorCtx, [OWNERS_CHAIR], Ct)).Should().BeFalse();
        // The actor's own chair is offered, so the trade completes and the owner's chair is seen
        // not to ride along.
        (await trades.AddItemsAsync(actorCtx, [ACTORS_CHAIR, OWNERS_CHAIR], Ct))
            .Should()
            .BeTrue();
        await trades.AcceptAsync(actorCtx, true, Ct);
        await trades.AcceptAsync(partnerCtx, true, Ct);
        await trades.ConfirmAsync(actorCtx, true, Ct);
        await trades.ConfirmAsync(partnerCtx, true, Ct);

        OwnerOf(ACTORS_CHAIR).Should().Be(PARTNER, "the trade went through");
        OwnerOf(OWNERS_CHAIR).Should().Be(OWNER);
        (await owner.GetItemSnapshotAsync(OWNERS_CHAIR, Ct)).Should().NotBeNull();
    }

    private async Task<IInventoryGrain> InventoryOfAsync(int playerId)
    {
        var inventory = await _world.NewInventoryAsync(playerId, Ct);

        _world.Fakes.Instances[(typeof(IInventoryGrain), (long)playerId)] = inventory;

        return inventory;
    }

    /// <summary>The <c>PlaceObject</c> message from the actor, standing in a room they own.</summary>
    private Task PlaceAsync(string data) =>
        new PlaceObjectMessageHandler(
            new RoomService(
                NullLogger<IRoomService>.Instance,
                Options.Create(new RoomConfig()),
                _world.Fakes.Create<IGrainFactory>(),
                TimeProvider.System
            )
        )
            .HandleAsync(
                new PlaceObjectMessage { Data = data },
                new MessageContext(_world.Fakes.Create<ISessionContext>(), ACTOR, ROOM),
                Ct
            )
            .AsTask();

    /// <summary>The items the room was asked to place, by the given placement.</summary>
    private IEnumerable<int> Placed(string placement) =>
        _world
            .Fakes.Log.Of(placement)
            .Select(call => ((FurnitureItemSnapshot)call.Args[1]!).ItemId.Value);

    private int OwnerOf(int itemId) => _world.Row(itemId).PlayerEntityId;

    private IRoomTradeGrain NewTradeGrain()
    {
        _world.Fakes.Handlers[nameof(IRoomGrain.GetTradePartiesAsync)] = _ =>
            Task.FromResult<TradePartiesSnapshot?>(
                new TradePartiesSnapshot
                {
                    InitiatorId = ACTOR,
                    PartnerId = PARTNER,
                    PartnerName = "partner",
                    InitiatorMayTrade = true,
                    PartnerMayTrade = true,
                }
            );
        _world.Fakes.Handlers["HasAsync"] = _ => Task.FromResult(true);
        // Both own the trading pass badge.
        _world.Fakes.Handlers["HasBadgeAsync"] = _ => Task.FromResult(true);

        var grain = GrainHarness.Create<RoomTradeGrain>(_world.Fakes);
        GrainHarness.Replace(grain, new RoomTradeLiveState { RoomId = ROOM });

        return (IRoomTradeGrain)grain;
    }
}
