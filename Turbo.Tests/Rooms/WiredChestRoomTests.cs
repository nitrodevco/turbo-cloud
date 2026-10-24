using System.Collections.Immutable;
using FluentAssertions;
using Orleans.Streams;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Grains;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Grains.WiredTrading;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Who may do what with a wired chest in a room, and the trade window that fills one:
/// a chest is locked when placed, only its owner takes from a plain chest, and a deposit goes
/// through only once accepted and confirmed, with only what the chest takes.
/// </summary>
public class WiredChestRoomTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const int CHEST = 7;
    private const int OWNER = 1;
    private const int GUEST = 5;

    [Fact]
    public async Task A_chest_is_locked_when_placed()
    {
        var (room, chest) = CreateRoomWithChest();

        await chest.OnPlaceAsync(ActionContext.CreateForPlayer(OWNER, 1), Ct);

        chest.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task The_owner_upgrades_a_coin_chest_to_a_wired_chest()
    {
        // ChestSettingsUI only fills the preview dropdowns for a furni chest; a coin chest's save
        // carries them unset, as -1.
        var (room, chest) = CreateRoomWithChest(coins: true);
        AddPlayer(room, OWNER);
        StubSummary(room);

        var done = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            CHEST,
            Preferences(previewMode: (WiredChestPreviewMode)(-1), previewAmount: -1, wired: true),
            Ct
        );

        done.Should().BeTrue();
        chest.IsWiredEnabled.Should().BeTrue();
        room.Fakes.Sent<ChestPreferencesUpdateSuccessMessageComposer>(SendRoute.Direct)
            .Should()
            .ContainSingle(x => x.ChestId == CHEST);
    }

    [Fact]
    public async Task A_furni_chest_saves_with_its_preview_amount_unpicked()
    {
        var (room, chest) = CreateRoomWithChest();
        AddPlayer(room, OWNER);
        StubSummary(room);

        var done = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            CHEST,
            Preferences(previewMode: WiredChestPreviewMode.None, previewAmount: 0, wired: false),
            Ct
        );

        done.Should().BeTrue();
        chest.Settings.PreviewAmount.Should().Be(1);
        room.Fakes.Sent<ChestPreferencesUpdateSuccessMessageComposer>(SendRoute.Direct)
            .Should()
            .ContainSingle(x => x.ChestId == CHEST);
    }

    [Theory]
    [InlineData("bad chest", "", "wiredfurni.error.unacceptable_chest_name")]
    [InlineData("chest", "a bad one", "wiredfurni.error.unacceptable_chest_desc")]
    public async Task A_name_or_description_the_filter_would_change_is_refused_with_Habbos_text(
        string name,
        string description,
        string errorKey
    )
    {
        var (room, _) = CreateRoomWithChest();
        AddPlayer(room, OWNER);
        StubSummary(room);
        room.Room.UseWordFilter(new BadIsUncleanFilter());

        var done = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            CHEST,
            Preferences(
                previewMode: WiredChestPreviewMode.None,
                previewAmount: 1,
                wired: false
            ) with
            {
                Name = name,
                Description = description,
            },
            Ct
        );

        done.Should().BeFalse();
        room.Fakes.Sent<WiredValidationErrorEventMessageComposer>(SendRoute.Direct)
            .Should()
            .ContainSingle(x => x.LocalizationKey == errorKey);
        room.Fakes.Sent<ChestPreferencesUpdateSuccessMessageComposer>(SendRoute.Direct)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task A_guest_cannot_take_from_a_plain_chest()
    {
        var (room, _) = CreateRoomWithChest();
        AddPlayer(room, GUEST);

        var done = await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(GUEST, 1),
            CHEST,
            new WithdrawFromChestInteraction { Amount = null, ItemType = null },
            Ct
        );

        done.Should().BeFalse();
        room.Fakes.Log.Of(nameof(IWiredChestGrain.WithdrawAsync)).Should().BeEmpty();
    }

    [Fact]
    public async Task The_owner_takes_from_the_chest_and_is_told_how_it_went()
    {
        var (room, _) = CreateRoomWithChest();
        AddPlayer(room, OWNER);
        room.Fakes.Handlers[nameof(IWiredChestGrain.WithdrawAsync)] = _ =>
            Task.FromResult(
                WiredChestMoveResultSnapshot.Failed(
                    WiredTransactionFailureType.Empty,
                    WiredChestSummarySnapshot.Empty
                )
            );

        await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            CHEST,
            new WithdrawFromChestInteraction { Amount = null, ItemType = null },
            Ct
        );

        room.Fakes.Log.Of(nameof(IWiredChestGrain.WithdrawAsync)).Should().ContainSingle();
        room.Fakes.Sent<WiredTransactionFailMessageComposer>(SendRoute.Direct)
            .Should()
            .ContainSingle(x => x.FailureType == WiredTransactionFailureType.Empty);
    }

    [Fact]
    public async Task Opening_a_chest_closes_the_one_the_player_had_open()
    {
        const int OTHER_CHEST = 8;
        var (room, _) = CreateRoomWithChest();
        room.AddToRoom(CreateChest(room, OTHER_CHEST, 3));
        AddPlayer(room, OWNER);
        // Each chest now has a viewer, so the first counts as open when the second opens.
        room.Fakes.Handlers[nameof(IWiredChestGrain.OpenAsync)] = _ => Task.FromResult(1);

        await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            CHEST,
            new OpenChestInteraction(),
            Ct
        );
        await room.Room.InteractWithItemAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            OTHER_CHEST,
            new OpenChestInteraction(),
            Ct
        );

        room.Fakes.Log.Of(nameof(IWiredChestGrain.CloseAsync))
            .Should()
            .ContainSingle(x => Equals(x.Key, (long)CHEST) && x.Args.Contains((PlayerId)OWNER));
    }

    [Fact]
    public async Task A_furni_chest_trade_takes_furni_from_the_inventory()
    {
        // An inventory item sits in no room: its snapshot's room id is -1, as the inventory writes it.
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        fakes.Handlers["GetItemSnapshotsAsync"] = _ => Task.FromResult(Offer("rare_dragonlamp"));

        await trade.StartChestDepositAsync(1, CHEST, WiredChestKind.Furni, 1000, Ct);
        await trade.AddItemsAsync([20], Ct);

        fakes
            .Sent<WiredTradeTransactionNotificationMessageComposer>(SendRoute.Direct)
            .Should()
            .BeEmpty();
        fakes
            .Sent<WiredTradeItemsUpdateMessageComposer>(SendRoute.Direct)
            .Last()
            .FirstItems.Should()
            .ContainSingle();
    }

    [Fact]
    public async Task A_furni_chest_trade_does_not_take_credit_furni()
    {
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        fakes.Handlers["GetItemSnapshotsAsync"] = _ => Task.FromResult(Offer("CF_10_goldbar"));

        await trade.StartChestDepositAsync(1, CHEST, WiredChestKind.Furni, 1000, Ct);
        await trade.AddItemsAsync([20], Ct);

        fakes
            .Sent<WiredTradeTransactionNotificationMessageComposer>(SendRoute.Direct)
            .Should()
            .ContainSingle(x => x.Error == WiredTradeErrorType.InvalidItem);
        fakes
            .Sent<WiredTradeItemsUpdateMessageComposer>(SendRoute.Direct)
            .Last()
            .FirstItems.Should()
            .BeEmpty();
    }

    [Fact]
    public async Task A_deposit_offer_stops_at_the_chests_free_capacity()
    {
        // Habbo's wired_transactions.notification.trade_error.9: "Exceeds the chest's capacity".
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        fakes.Handlers["GetItemSnapshotsAsync"] = _ => Task.FromResult(Offer("chair", 20, 21, 22));

        await trade.StartChestDepositAsync(1, CHEST, WiredChestKind.Furni, 2, Ct);
        await trade.AddItemsAsync([20, 21, 22], Ct);

        fakes
            .Sent<WiredTradeTransactionNotificationMessageComposer>(SendRoute.Direct)
            .Should()
            .ContainSingle(x => x.Error == WiredTradeErrorType.ExceedsChestCapacity);
        fakes
            .Sent<WiredTradeItemsUpdateMessageComposer>(SendRoute.Direct)
            .Last()
            .FirstItems.Should()
            .HaveCount(2);
    }

    [Fact]
    public async Task A_credit_chest_deposit_offer_counts_the_credits_against_its_free_capacity()
    {
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        fakes.Handlers["GetItemSnapshotsAsync"] = _ =>
            Task.FromResult(Offer("CF_10_goldbar", 20, 21));

        await trade.StartChestDepositAsync(1, CHEST, WiredChestKind.Coins, 15, Ct);
        await trade.AddItemsAsync([20, 21], Ct);

        fakes
            .Sent<WiredTradeTransactionNotificationMessageComposer>(SendRoute.Direct)
            .Should()
            .ContainSingle(x => x.Error == WiredTradeErrorType.ExceedsChestCapacity);
        fakes
            .Sent<WiredTradeItemsUpdateMessageComposer>(SendRoute.Direct)
            .Last()
            .FirstItems.Should()
            .ContainSingle();
    }

    [Fact]
    public async Task A_trade_is_deposited_only_after_it_was_accepted_and_confirmed()
    {
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        fakes.Handlers["GetItemSnapshotsAsync"] = _ => Task.FromResult(Offer("chair"));
        fakes.Handlers[nameof(IRoomGrainDeposit.DepositIntoWiredChestAsync)] = _ =>
            Task.FromResult(
                new WiredChestMoveResultSnapshot
                {
                    Failure = null,
                    TransactionId = 1,
                    Coins = 0,
                    Items = [],
                    Summary = WiredChestSummarySnapshot.Empty,
                }
            );

        await trade.StartChestDepositAsync(1, CHEST, WiredChestKind.Furni, 1000, Ct);
        await trade.AddItemsAsync([20], Ct);

        await trade.ConfirmAsync(isFinalConfirm: false, Ct);
        fakes.Log.Of(nameof(IRoomGrainDeposit.DepositIntoWiredChestAsync)).Should().BeEmpty();

        await trade.ConfirmAsync(isFinalConfirm: true, Ct);

        var deposit = fakes.Log.Of(nameof(IRoomGrainDeposit.DepositIntoWiredChestAsync)).Single();
        ((ImmutableArray<RoomObjectId>)deposit.Args[2]!).Should().Equal((RoomObjectId)20);
        fakes.Sent<WiredTradeCompletedMessageComposer>(SendRoute.Direct).Should().ContainSingle();
    }

    [Fact]
    public async Task A_deposit_confirmed_before_it_was_accepted_is_cancelled_as_invalid()
    {
        var fakes = new Fakes();
        var trade = TradeGrain(fakes);
        fakes.Handlers["GetItemSnapshotsAsync"] = _ => Task.FromResult(Offer("chair"));

        await trade.StartChestDepositAsync(1, CHEST, WiredChestKind.Furni, 1000, Ct);
        await trade.AddItemsAsync([20], Ct);
        await trade.ConfirmAsync(isFinalConfirm: true, Ct);

        fakes
            .Sent<WiredTradeCancelledMessageComposer>(SendRoute.Direct)
            .Should()
            .ContainSingle(x => x.FailureType == WiredTransactionFailureType.Invalid);

        // The trade is over: what was offered stays in the inventory.
        await trade.ConfirmAsync(isFinalConfirm: false, Ct);
        await trade.ConfirmAsync(isFinalConfirm: true, Ct);
        fakes.Log.Of(nameof(IRoomGrainDeposit.DepositIntoWiredChestAsync)).Should().BeEmpty();
    }

    /// <summary>Names the room grain method the trade grain calls; nameof needs a type.</summary>
    private interface IRoomGrainDeposit
    {
        public Task DepositIntoWiredChestAsync();
    }

    private static IWiredTradeGrain TradeGrain(Fakes fakes) =>
        (IWiredTradeGrain)GrainHarness.Create<WiredTradeGrain>(fakes, playerId: GUEST);

    private static ImmutableArray<FurnitureItemSnapshot> Offer(string name, params int[] itemIds) =>
        [.. (itemIds.Length > 0 ? itemIds : [20]).Select(id => Item(name, id))];

    private static FurnitureItemSnapshot Item(string name, int itemId) =>
        new FurnitureItemSnapshot
        {
            ItemId = itemId,
            SpriteId = 20,
            OwnerId = GUEST,
            OwnerName = "guest",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 20,
                SpriteId = 20,
                Name = name,
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.Default,
                LogicName = "default_floor",
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
                UsagePolicy = FurnitureUsageType.Everybody,
                ExtraData = null,
            },
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "" },
            ExtraData = "",
            SecondsToExpiration = 0,
            HasRentPeriodStarted = false,
            RoomId = -1,
        };

    private static void AddPlayer(RoomHarness room, int playerId)
    {
        room.AddPlayer(playerId);
        room.Fakes.Handlers["get_Name"] = call =>
            call.Interface == typeof(IRoomPlayer) && call.Key is int id
                ? $"player{id}"
                : Fakes.NotHandled;
    }

    private static void StubSummary(RoomHarness room) =>
        room.Fakes.Handlers[nameof(IWiredChestGrain.GetSummaryAsync)] = _ =>
            Task.FromResult(WiredChestSummarySnapshot.Empty);

    private static SetChestPreferencesInteraction Preferences(
        WiredChestPreviewMode previewMode,
        int previewAmount,
        bool wired
    ) =>
        new()
        {
            Name = "chest",
            Description = "",
            EveryoneCanOpen = false,
            EveryoneCanDonate = false,
            StateMode = default,
            PreviewMode = previewMode,
            PreviewAmount = previewAmount,
            WiredEnabled = wired,
        };

    private static (RoomHarness Room, FurnitureWiredChestLogic Chest) CreateRoomWithChest(
        bool coins = false
    )
    {
        var room = new RoomHarness();

        // The chest tells the room when its stuff data changes.
        room.Room._roomOutbound = room.Fakes.Create<IAsyncStream<RoomOutboundSnapshot>>(
            "room-stream"
        );

        var item = coins ? CreateCoinChest(room, CHEST, 2) : CreateChest(room, CHEST, 2);

        room.AddToRoom(item);

        return (room, (FurnitureWiredChestLogic)item.Logic);
    }

    private static RoomFloorItem CreateChest(RoomHarness room, int id, int x) =>
        room.CreateFloorItem(
            id,
            x,
            2,
            Altitude.Zero,
            name: "wf_storage_furni1",
            logic: "wired_chest_furni",
            createLogic: (stuffDataFactory, ctx) =>
                new FurnitureWiredFurniChestLogic(stuffDataFactory, ctx)
        );

    private sealed class BadIsUncleanFilter : IWordFilter
    {
        public string Replacement => "bobba";

        public string Filter(string text) => text.Replace("bad", Replacement);

        public string Filter(string text, IReadOnlySet<string> extraWords) => Filter(text);

        public bool IsClean(string text) => !text.Contains("bad");

        public Task ReloadAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static RoomFloorItem CreateCoinChest(RoomHarness room, int id, int x) =>
        room.CreateFloorItem(
            id,
            x,
            2,
            Altitude.Zero,
            name: "wf_storage_coins1",
            logic: "wired_chest_coins",
            createLogic: (stuffDataFactory, ctx) =>
                new FurnitureWiredCoinsChestLogic(stuffDataFactory, ctx)
        );
}
