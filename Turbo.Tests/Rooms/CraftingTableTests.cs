using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Crafting;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Incoming.Crafting;
using Turbo.Primitives.Messages.Outgoing.Crafting;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The crafting table as Flash's CraftingWidgetHandler drives it: the listed recipes and the furni
/// classes the mixer takes, what a recipe needs, crafting it from the owner's inventory, and the
/// mixer finding a secret recipe. Only the table's owner crafts; rented furni are no ingredients.
/// </summary>
public sealed class CraftingTableTests
{
    private const int TABLE = 50;
    private const int OWNER_AVATAR = 1;
    private const int GUEST_AVATAR = 2;
    private const int OWNER = 100 + OWNER_AVATAR;
    private const int GUEST = 100 + GUEST_AVATAR;

    private const string RECIPES = """
        {"crafting":{"recipes":[
            {"code":"chair_recipe","product":"craft_chair","ingredients":[{"furni":"plank","amount":2},{"furni":"nail"}]},
            {"code":"secret_throne","product":"craft_throne","productCode":"throne_product","secret":true,"ingredients":[{"furni":"plank","amount":2},{"furni":"gold"}]},
            {"code":"secret_stool","product":"craft_stool","secret":true,"ingredients":[{"furni":"plank"},{"furni":"nail"}]}
        ]}}
        """;

    private static readonly string[] CLASSES =
    [
        "plank",
        "nail",
        "gold",
        "craft_chair",
        "craft_throne",
        "craft_stool",
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(10, 10);
    private readonly Dictionary<long, Dictionary<int, (string Class, bool Rented)>> _inventories =
        new() { [OWNER] = [], [GUEST] = [] };
    private int _nextItemId = 500;

    /// <summary>An item that leaves the inventory (traded, sold) just before the craft uses it up.</summary>
    private int? _goneBeforeConsume;

    public CraftingTableTests()
    {
        var fakes = _room.Harness.Fakes;

        fakes.Handlers["TryGetDefinitionByName"] = call =>
            Array.IndexOf(CLASSES, (string)call.Args[0]!) is var i and >= 0
                ? Definition(2000 + i, CLASSES[i])
                : null;
        fakes.Handlers["GetAllItemSnapshotsAsync"] = call =>
            Task.FromResult(
                _inventories[Key(call)].Select(x => Snapshot(x.Key, x.Value)).ToImmutableArray()
            );
        fakes.Handlers["GetItemSnapshotsAsync"] = call =>
        {
            var inventory = _inventories[Key(call)];

            return Task.FromResult(
                ((ImmutableArray<RoomObjectId>)call.Args[0]!)
                    .Where(x => inventory.ContainsKey(x.Value))
                    .Select(x => Snapshot(x.Value, inventory[x.Value]))
                    .ToImmutableArray()
            );
        };
        fakes.Handlers["ConsumeFurnitureAsync"] = call =>
        {
            var id = ((RoomObjectId)call.Args[0]!).Value;

            TakeAwayGone(call);

            return Task.FromResult(
                _inventories[Key(call)].Remove(id, out var item) ? Snapshot(id, item) : null
            );
        };
        fakes.Handlers["ConsumeAllFurnitureAsync"] = call =>
        {
            var ids = ((ImmutableArray<RoomObjectId>)call.Args[0]!).Select(x => x.Value).ToList();
            var inventory = _inventories[Key(call)];

            TakeAwayGone(call);

            if (!ids.All(inventory.ContainsKey))
                return Task.FromResult(ImmutableArray<FurnitureItemSnapshot>.Empty);

            return Task.FromResult(
                ids.Select(id =>
                    {
                        inventory.Remove(id, out var item);

                        return Snapshot(id, item);
                    })
                    .ToImmutableArray()
            );
        };
        fakes.Handlers["GrantFurnitureAsync"] = call =>
        {
            var id = _nextItemId++;
            var item = (CLASSES[(int)call.Args[0]! - 2000], false);

            _inventories[Key(call)][id] = item;

            return Task.FromResult<FurnitureItemSnapshot?>(Snapshot(id, item));
        };

        _room.Enter(OWNER_AVATAR, 3, 4);
        _room.Enter(GUEST_AVATAR, 4, 4);
        AddTable();
    }

    [Fact]
    public async Task The_table_lists_its_recipes_and_what_they_take()
    {
        (await InteractAsync(GUEST, new GetCraftableProductsInteraction())).Should().BeTrue();

        var products = _room
            .Harness.Fakes.SentToPlayer<CraftableProductsMessageComposer>(GUEST)
            .Single();

        // Secret recipes are found in the mixer, not listed; their ingredients still go in it.
        products
            .Products.Should()
            .Equal(
                new CraftableProductSnapshot
                {
                    RecipeCode = "chair_recipe",
                    ProductCode = "craft_chair",
                    FurnitureClassName = "craft_chair",
                }
            );
        products.UsableFurniClasses.Should().Equal("plank", "nail", "gold");

        (
            await _room.Harness.Room.SendCraftingRecipeAsync(
                ActionContext.CreateForPlayer(GUEST, 1),
                "chair_recipe",
                Ct
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.Harness.Room.SendCraftingRecipeAsync(
                ActionContext.CreateForPlayer(GUEST, 1),
                "secret_throne",
                Ct
            )
        )
            .Should()
            .BeFalse("a secret recipe is not given away");

        _room
            .Harness.Fakes.SentToPlayer<CraftingRecipeMessageComposer>(GUEST)
            .Single()
            .Ingredients.Select(x => (x.Count, x.FurnitureClassName))
            .Should()
            .Equal((2, "plank"), (1, "nail"));
    }

    [Fact]
    public async Task The_owner_crafts_a_listed_recipe_from_owned_furni_only()
    {
        Give(OWNER, "plank", rented: true);
        Give(OWNER, "plank");
        Give(OWNER, "nail");
        Give(GUEST, "plank");
        Give(GUEST, "plank");
        Give(GUEST, "nail");

        // A rented plank is no ingredient: one owned plank is short.
        await InteractAsync(OWNER, new CraftInteraction { RecipeCode = "chair_recipe" });
        Give(OWNER, "plank");
        // Not the guest's table.
        await InteractAsync(GUEST, new CraftInteraction { RecipeCode = "chair_recipe" });
        await InteractAsync(OWNER, new CraftInteraction { RecipeCode = "chair_recipe" });

        Results(OWNER).Should().Equal(null, "craft_chair");
        Results(GUEST).Should().Equal([null]);
        _inventories[OWNER]
            .Values.Should()
            .BeEquivalentTo([("plank", true), ("craft_chair", false)]);
        _inventories[GUEST].Values.Select(x => x.Class).Should().Equal("plank", "plank", "nail");
    }

    [Fact]
    public async Task The_mixer_finds_a_secret_recipe_and_crafts_it()
    {
        var plank1 = Give(OWNER, "plank");
        var plank2 = Give(OWNER, "plank");
        var gold = Give(OWNER, "gold");
        var nail = Give(OWNER, "nail");

        // One plank: part of the throne and the stool, neither exact.
        await MixAsync([plank1], craft: false);
        // Plank and nail: the stool exactly, nothing else holds both.
        await MixAsync([plank1, nail], craft: false);
        // Two planks: only the throne holds two.
        await MixAsync([plank1, plank2], craft: false);
        // Gold and nail: no recipe has both.
        await MixAsync([gold, nail], craft: false);

        _room
            .Harness.Fakes.SentToPlayer<CraftingRecipesAvailableMessageComposer>(OWNER)
            .Select(x => (x.Count, x.RecipeComplete))
            .Should()
            .Equal((2, false), (0, true), (1, false), (0, false));

        await MixAsync([plank1, gold], craft: true);
        await MixAsync([plank1, plank2, gold], craft: true);

        Results(OWNER).Should().Equal(null, "craft_throne");
        _room
            .Harness.Fakes.SentToPlayer<CraftingResultMessageComposer>(OWNER)
            .Last()
            .Product!.ProductCode.Should()
            .Be("throne_product");
        _inventories[OWNER]
            .Values.Select(x => x.Class)
            .Should()
            .BeEquivalentTo("nail", "craft_throne");
    }

    // The mix was checked, then its ingredients used up one at a time: one that had left the
    // inventory in between (traded, sold) failed the craft after the ones before it were gone.
    [Fact]
    public async Task An_ingredient_gone_before_the_craft_uses_up_none_of_the_others()
    {
        var plank = Give(OWNER, "plank");
        var nail = Give(OWNER, "nail");
        _goneBeforeConsume = nail;

        await MixAsync([plank, nail], craft: true);

        Results(OWNER).Should().Equal([null]);
        _inventories[OWNER].Keys.Should().Equal(plank);
    }

    // CraftingWidgetHandler.registerForFurniListInvalidate: after a craft the widget waits for the
    // inventory's list to go stale before it takes presses again; a failure stops the wait itself.
    [Fact]
    public async Task A_craft_calls_the_crafters_inventory_list_stale_after_its_result()
    {
        var plank = Give(OWNER, "plank");
        var nail = Give(OWNER, "nail");

        await MixAsync([plank], craft: true);

        _room
            .Harness.Fakes.SentToPlayer<FurniListInvalidateEventMessageComposer>(OWNER)
            .Should()
            .BeEmpty();

        await MixAsync([plank, nail], craft: true);

        var sent = _room.Harness.Fakes.SentToPlayer<IComposer>(OWNER).ToList();

        sent.OfType<FurniListInvalidateEventMessageComposer>().Should().ContainSingle();
        sent.FindIndex(x => x is FurniListInvalidateEventMessageComposer)
            .Should()
            .BeGreaterThan(sent.FindLastIndex(x => x is CraftingResultMessageComposer));
        _room
            .Harness.Fakes.SentToPlayer<FurniListInvalidateEventMessageComposer>(GUEST)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void Crafting_packets_are_read_and_written_as_Flash_does()
    {
        PacketHarness
            .Parse(
                PacketHarness.Incoming("CraftSecretMessageEvent"),
                PacketHarness.Payload(w => w.Int(50).Int(2).Int(7).Int(8))
            )
            .Should()
            .BeEquivalentTo(new CraftSecretMessage { ObjectId = 50, ItemIds = [7, 8] });
        PacketHarness
            .Parse(
                PacketHarness.Incoming("CraftMessageEvent"),
                PacketHarness.Payload(w => w.Int(50).String("chair"))
            )
            .Should()
            .Be(new CraftMessage { ObjectId = 50, RecipeCode = "chair" });

        var products = PacketHarness.Encode(
            new CraftableProductsMessageComposer
            {
                Products =
                [
                    new CraftableProductSnapshot
                    {
                        RecipeCode = "r",
                        ProductCode = "p",
                        FurnitureClassName = "f",
                    },
                ],
                UsableFurniClasses = ["a"],
            }
        );
        var failed = PacketHarness.Encode(new CraftingResultMessageComposer { Success = false });

        // CraftableProductsMessageParser.as:18 and FurnitureProductItem.as:13.
        Convert
            .ToHexString(products.PopBytes(products.Remaining))
            .Should()
            .Be("00000001" + "000172" + "000170" + "000166" + "00000001" + "000161");
        // CraftingResultMessageParser.as:13: no product after a failure.
        Convert.ToHexString(failed.PopBytes(failed.Remaining)).Should().Be("00");
    }

    private Task<bool> InteractAsync(int playerId, FurnitureInteraction interaction) =>
        _room.Harness.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(playerId, 1),
            TABLE,
            interaction,
            Ct
        );

    private Task<bool> MixAsync(int[] itemIds, bool craft) =>
        InteractAsync(
            OWNER,
            new CraftingMixerInteraction
            {
                ItemIds = [.. itemIds.Select(x => (RoomObjectId)x)],
                Craft = craft,
            }
        );

    private IEnumerable<string?> Results(int playerId) =>
        _room
            .Harness.Fakes.SentToPlayer<CraftingResultMessageComposer>(playerId)
            .Select(x => x.Success ? x.Product!.FurnitureClassName : null);

    private void TakeAwayGone(Turbo.Tests.Support.FakeCall call)
    {
        if (_goneBeforeConsume is { } gone)
            _inventories[Key(call)].Remove(gone);

        _goneBeforeConsume = null;
    }

    private int Give(int playerId, string furniClass, bool rented = false)
    {
        var id = _nextItemId++;

        _inventories[playerId][id] = (furniClass, rented);

        return id;
    }

    private static long Key(Turbo.Tests.Support.FakeCall call) => Convert.ToInt64(call.Key);

    private static FurnitureItemSnapshot Snapshot(int id, (string Class, bool Rented) item) =>
        new()
        {
            ItemId = id,
            SpriteId = 1,
            OwnerId = 0,
            OwnerName = "",
            Definition = Definition(2000 + Array.IndexOf(CLASSES, item.Class), item.Class),
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "" },
            ExtraData = "",
            SecondsToExpiration = item.Rented ? 3600 : -1,
            HasRentPeriodStarted = item.Rented,
            RoomId = 0,
        };

    private static FurnitureDefinitionSnapshot Definition(
        int id,
        string name,
        string? extraData = null
    ) =>
        new()
        {
            Id = id,
            SpriteId = id,
            Name = name,
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            LogicName = extraData is null ? "default" : "crafting",
            TotalStates = 1,
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
            UsagePolicy = FurnitureUsageType.Nobody,
            ExtraData = extraData,
        };

    private void AddTable()
    {
        var item = new RoomFloorItem
        {
            ObjectId = TABLE,
            OwnerId = OWNER,
            OwnerName = "owner",
            Definition = Definition(1000, "crafting_table", RECIPES),
        };

        item.SetExtraData(null);
        item.SetPosition(3, 3);
        item.SetPositionZ(Altitude.Zero);
        item.SetRotation(Rotation.North);
        item.SetLogic(
            new FurnitureCraftingLogic(
                _room.StuffData,
                new RoomFloorItemContext(_room.Harness.Room, item)
            )
        );

        _room.Harness.State.ItemsById.Add((RoomObjectId)TABLE, item);
        _room.Map.AddItem(item);
    }
}
