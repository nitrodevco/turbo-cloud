using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Extensions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains.Subscriptions;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Habbo lends its stack helper tile without counting it. Official client capture (build 1745):
/// every successful Builders Club place and pickup is answered with BuildersClubFurniCount (178
/// of 178), except tile_stackmagic, whose 2 places and 2 pickups got none, and a table placed
/// after the tile was counted as the first furni. The other stack tile sizes share its logic, so
/// leaving the logic out (rather than the one furni) is inference.
/// </summary>
public sealed class BuildersClubUncountedLogicTests
{
    private const int OWNER = 1;
    private const int TILE_OFFER = 17911;
    private const int TABLE_OFFER = 17912;
    private const int TILE_DEFINITION = 5103;
    private const int TABLE_DEFINITION = 5104;
    private const string STACK_TILE = "custom_stack_height";
    private const string TABLE = "default";

    private readonly WiredRoom _room = new(8, 8);
    private readonly InMemoryDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_stack_tile_borrow_is_not_counted_and_not_reported()
    {
        var grain = await NewGrainAsync();

        await grain.OnBorrowedAsync(OWNER, STACK_TILE, Ct);

        (await grain.GetBorrowedCountAsync(OWNER, Ct)).Should().Be(0);
        CountsSent().Should().BeEmpty();

        await grain.OnBorrowedAsync(OWNER, TABLE, Ct);

        (await grain.GetBorrowedCountAsync(OWNER, Ct)).Should().Be(1);
        CountsSent().Should().Equal(1);
    }

    [Fact]
    public async Task Returns_count_only_what_was_counted()
    {
        var grain = await NewGrainAsync();
        await grain.OnBorrowedAsync(OWNER, TABLE, Ct);
        await grain.OnBorrowedAsync(OWNER, TABLE, Ct);

        await grain.OnReturnedAsync(OWNER, [STACK_TILE], Ct);

        (await grain.GetBorrowedCountAsync(OWNER, Ct)).Should().Be(2);
        CountsSent().Should().Equal(1, 2);

        await grain.OnReturnedAsync(OWNER, [STACK_TILE, TABLE], Ct);

        (await grain.GetBorrowedCountAsync(OWNER, Ct)).Should().Be(1);
        CountsSent().Should().Equal(1, 2, 1);
    }

    [Fact]
    public async Task The_recount_leaves_borrowed_stack_tiles_out()
    {
        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            db.FurnitureDefinitions.AddRange(
                DefinitionEntity(TILE_DEFINITION, STACK_TILE),
                DefinitionEntity(TABLE_DEFINITION, TABLE)
            );
            db.BuildersClubFurnitures.AddRange(
                Row(1, TILE_DEFINITION, TILE_OFFER),
                Row(2, TABLE_DEFINITION, TABLE_OFFER)
            );
            await db.SaveChangesAsync(Ct);
        }

        var grain = await NewGrainAsync();

        (await grain.GetBorrowedCountAsync(OWNER, Ct)).Should().Be(1);
    }

    [Fact]
    public async Task At_a_limit_of_one_a_table_still_places_after_a_stack_tile()
    {
        var grain = await NewGrainAsync();
        _room.Harness.Fakes.Instances[(typeof(IBuildersClubGrain), SingletonGrainId.GLOBAL)] =
            grain;
        StubWarehouse(furniLimit: 1);

        (await PlaceAsync(TILE_OFFER, 2, 2)).Should().BeTrue();
        CountsSent().Should().BeEmpty("Habbo sends no count for the stack tile");

        (await PlaceAsync(TABLE_OFFER, 4, 4)).Should().BeTrue("the stack tile took no slot");
        CountsSent().Should().Equal(1);

        (await PlaceAsync(TABLE_OFFER, 5, 5)).Should().BeFalse("the table filled the limit");

        var tile = _room
            .Harness.Module<RoomFurniModule>()
            .Items.Single(x => x.Definition.Id == TILE_DEFINITION);
        await _room
            .Harness.Module<RoomObjectModule>()
            .RemoveObjectAsync(ActionContext.CreateForPlayer(OWNER, 1), tile, Ct);

        CountsSent().Should().Equal([1], "picking the stack tile up reports nothing either");
        (await grain.GetBorrowedCountAsync(OWNER, Ct)).Should().Be(1);
    }

    private async Task<IBuildersClubGrain> NewGrainAsync()
    {
        var grain = GrainHarness.Create(
            typeof(CatalogConfig).Assembly,
            "Turbo.Catalog.Grains.BuildersClubGrain",
            _room.Harness.Fakes,
            _db
        );
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IBuildersClubGrain)grain;
    }

    /// <summary>Every count the club sent the player, in order.</summary>
    private int[] CountsSent() =>
        [
            .. _room
                .Harness.Fakes.Log.Calls.SelectMany(x => x.Args)
                .OfType<BuildersClubFurniCountMessageComposer>()
                .Select(x => x.FurniCount),
        ];

    private Task<bool> PlaceAsync(int offerId, int x, int y) =>
        _room
            .Harness.Module<RoomFurniModule>()
            .PlaceBuildersClubFloorItemAsync(
                ActionContext.CreateForPlayer(OWNER, 1),
                1,
                offerId,
                string.Empty,
                x,
                y,
                Rotation.North,
                false,
                Ct
            );

    private void StubWarehouse(int furniLimit)
    {
        var definitions = new Dictionary<int, FurnitureDefinitionSnapshot>
        {
            [TILE_DEFINITION] = DefinitionEntity(TILE_DEFINITION, STACK_TILE).ToSnapshot(0.01),
            [TABLE_DEFINITION] = DefinitionEntity(TABLE_DEFINITION, TABLE).ToSnapshot(0.01),
        };
        var catalog = (CatalogSnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(CatalogSnapshot));
        RoomHarness.SetMember(
            catalog,
            "OffersById",
            ImmutableDictionary<int, CatalogOfferSnapshot>
                .Empty.Add(TILE_OFFER, Offer(TILE_DEFINITION))
                .Add(TABLE_OFFER, Offer(TABLE_DEFINITION))
        );
        var subscription = (PlayerSubscriptionSnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(PlayerSubscriptionSnapshot));
        RoomHarness.SetMember(subscription, "SecondsLeft", 3600);
        RoomHarness.SetMember(subscription, "FurniLimit", furniLimit);

        var fakes = _room.Harness.Fakes;
        fakes.Handlers["GetCatalogSnapshot"] = _ => catalog;
        fakes.Handlers["TryGetDefinition"] = call => definitions[(int)call.Args[0]!];
        fakes.Handlers["GetAsync"] = call =>
            call.Interface == typeof(IPlayerSubscriptionGrain)
                ? Task.FromResult(subscription)
                : Fakes.NotHandled;
        fakes.Handlers["CreateFromDefinition"] = call =>
        {
            var item = new RoomFloorItem
            {
                ObjectId = (Turbo.Primitives.Rooms.Object.RoomObjectId)call.Args[0]!,
                OwnerId = OWNER,
                OwnerName = string.Empty,
                Definition = (FurnitureDefinitionSnapshot)call.Args[2]!,
            };
            item.SetExtraData(null);

            return item;
        };
        fakes.Handlers["InsertBuildersClubItemAsync"] = _ => Task.FromResult(true);
    }

    private static CatalogOfferSnapshot Offer(int definitionId)
    {
        var product = (CatalogProductSnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(CatalogProductSnapshot));
        RoomHarness.SetMember(product, "ProductType", ProductType.Floor);
        RoomHarness.SetMember(product, "FurniDefinitionId", definitionId);
        var offer = (CatalogOfferSnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(CatalogOfferSnapshot));
        RoomHarness.SetMember(offer, "Visible", true);
        RoomHarness.SetMember(offer, "Products", ImmutableArray.Create(product));

        return offer;
    }

    private static FurnitureDefinitionEntity DefinitionEntity(int id, string logic) =>
        new()
        {
            Id = id,
            SpriteId = id,
            Name = logic == STACK_TILE ? "tile_stackmagic" : "table_plasto_square",
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            Logic = logic,
            Width = 1,
            Length = 1,
            StackHeight = logic == STACK_TILE ? 0 : 1,
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = false,
            CanTrade = false,
            CanGroup = false,
            CanSell = false,
        };

    private static BuildersClubFurnitureEntity Row(int objectId, int definitionId, int offerId) =>
        new()
        {
            RoomEntityId = 1,
            RoomObjectId = objectId,
            FurnitureDefinitionEntityId = definitionId,
            PlacedByPlayerEntityId = OWNER,
            CatalogOfferEntityId = offerId,
        };
}
