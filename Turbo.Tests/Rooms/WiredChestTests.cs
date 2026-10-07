using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Players;
using Turbo.Furniture.Providers;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Grains;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A wired chest keeps the furni put into it as the same rows (owned by the chest's owner and
/// in no inventory) and its credits as a balance, moves them out to whoever is given them, and
/// never takes more than it may hold.
/// </summary>
public sealed class WiredChestTests : IDisposable
{
    private const int OWNER = 1;
    private const int ALICE = 2;
    private const int CHEST = 100;

    private const int CHAIR_DEFINITION = 1;
    private const int GOLD_DEFINITION = 2;
    private const int CHEST_DEFINITION = 3;

    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly Dictionary<int, FurnitureDefinitionSnapshot> _definitions = new()
    {
        [CHAIR_DEFINITION] = Definition(CHAIR_DEFINITION, "chair"),
        [GOLD_DEFINITION] = Definition(GOLD_DEFINITION, "CF_10_goldbar"),
        [CHEST_DEFINITION] = Definition(CHEST_DEFINITION, "wf_storage_furni1"),
    };
    private readonly IInventoryFurnitureLoader _loader;

    public WiredChestTests()
    {
        _db.Insert(Player(OWNER, "owner"));
        _db.Insert(Player(ALICE, "alice"));

        foreach (var definition in _definitions.Values)
            _db.Insert(DefinitionEntity(definition));

        _db.Insert(Furni(CHEST, CHEST_DEFINITION, OWNER));

        _fakes.Handlers["TryGetDefinition"] = call =>
            call.Args[0] is int id ? _definitions.GetValueOrDefault(id) : Fakes.NotHandled;

        // The inventory hands out what it holds: the rows the player owns in no room.
        _fakes.Handlers["GetItemSnapshotsAsync"] = call =>
            Task.FromResult(InventorySnapshots((ImmutableArray<RoomObjectId>)call.Args[0]!));

        _loader = (IInventoryFurnitureLoader)
            Activator.CreateInstance(
                typeof(Turbo.Inventory.InventoryModule).Assembly.GetType(
                    "Turbo.Inventory.Factories.InventoryFurnitureLoader"
                )!,
                All,
                null,
                [
                    _db,
                    _fakes.Create<IFurnitureDefinitionProvider>(),
                    new StuffDataFactory(NullLogger<IStuffDataFactory>.Instance),
                    NullLogger<IInventoryFurnitureLoader>.Instance,
                ],
                null
            )!;
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Deposited_furni_belong_to_the_chest_and_leave_the_inventory()
    {
        _db.Insert(Furni(10, CHAIR_DEFINITION, ALICE));
        _db.Insert(Furni(11, CHAIR_DEFINITION, ALICE));
        var chest = await ChestAsync();

        var result = await chest.DepositAsync(Deposit(WiredChestKind.Furni, 10, 11), default);

        result.Failure.Should().BeNull();
        result.Summary.ItemCount.Should().Be(2);
        Rows(10, 11)
            .Should()
            .AllSatisfy(row =>
            {
                row.ChestItemEntityId.Should().Be(CHEST);
                row.PlayerEntityId.Should().Be(OWNER);
            });
        Released().Should().BeEquivalentTo([(RoomObjectId)10, (RoomObjectId)11]);
        // The owner's inventory holds the chest itself, not what is in it.
        (await _loader.LoadByPlayerIdAsync(OWNER, "owner", default))
            .Select(x => x.ItemId.Value)
            .Should()
            .Equal(CHEST);
    }

    [Fact]
    public async Task A_deposit_past_the_capacity_moves_nothing()
    {
        _db.Insert(Furni(10, CHAIR_DEFINITION, ALICE));
        _db.Insert(Furni(11, CHAIR_DEFINITION, ALICE));
        var chest = await ChestAsync();

        var result = await chest.DepositAsync(
            Deposit(WiredChestKind.Furni, 10, 11) with
            {
                Capacity = 1,
            },
            default
        );

        result.Failure.Should().Be(WiredTransactionFailureType.ExceedsCapacity);
        Rows(10, 11).Should().AllSatisfy(row => row.ChestItemEntityId.Should().BeNull());
        Released().Should().BeEmpty();
    }

    [Fact]
    public async Task A_furni_chest_does_not_take_credit_furni()
    {
        _db.Insert(Furni(20, GOLD_DEFINITION, ALICE));
        var chest = await ChestAsync();

        var result = await chest.DepositAsync(Deposit(WiredChestKind.Furni, 20), default);

        result.Failure.Should().Be(WiredTransactionFailureType.Invalid);
        Rows(20).Single().ChestItemEntityId.Should().BeNull();
    }

    [Fact]
    public async Task Withdrawn_furni_go_to_the_receiver_oldest_first()
    {
        _db.Insert(Furni(10, CHAIR_DEFINITION, ALICE));
        _db.Insert(Furni(11, CHAIR_DEFINITION, ALICE));
        var chest = await ChestAsync();
        await chest.DepositAsync(Deposit(WiredChestKind.Furni, 10), default);
        await chest.DepositAsync(Deposit(WiredChestKind.Furni, 11), default);

        var result = await chest.WithdrawAsync(
            Withdraw(WiredChestKind.Furni, 1, WiredChestIterationMode.FirstInFirstOut),
            default
        );

        result.Failure.Should().BeNull();
        result.Summary.ItemCount.Should().Be(1);
        var given = Rows(10).Single();
        given.ChestItemEntityId.Should().BeNull();
        given.PlayerEntityId.Should().Be(ALICE);
        Rows(11).Single().ChestItemEntityId.Should().Be(CHEST);
        _fakes.Log.Of("ReceiveFurnitureAsync").Should().ContainSingle();
    }

    [Fact]
    public async Task Credit_furni_become_the_chest_balance_and_leave_through_the_wallet()
    {
        _db.Insert(Furni(20, GOLD_DEFINITION, ALICE));
        var chest = await ChestAsync();

        var deposit = await chest.DepositAsync(Deposit(WiredChestKind.Coins, 20), default);

        deposit.Failure.Should().BeNull();
        deposit.Summary.Coins.Should().Be(10);
        Rows(20).Should().BeEmpty();

        _fakes.Handlers["CreditAsync"] = call =>
            call.Args.Length == 4 ? Task.FromResult(WalletCreditResult.Applied) : Fakes.NotHandled;

        var withdrawal = await chest.WithdrawAsync(
            Withdraw(WiredChestKind.Coins, 4, WiredChestIterationMode.FirstInFirstOut),
            default
        );

        withdrawal.Failure.Should().BeNull();
        withdrawal.Summary.Coins.Should().Be(6);
        var credit = _fakes.Log.Of("CreditAsync").Single();
        credit.Args[1].Should().Be(4);
        credit.Args[2].Should().Be($"wiredchest:{withdrawal.TransactionId}");

        // A fresh grain reads the balance the database kept.
        (await (await ChestAsync()).GetSummaryAsync(Settings(WiredChestKind.Coins), default))
            .Coins.Should()
            .Be(6);
    }

    [Fact]
    public async Task A_withdrawal_the_wallet_refuses_puts_the_credits_back()
    {
        _db.Insert(Furni(20, GOLD_DEFINITION, ALICE));
        var chest = await ChestAsync();
        await chest.DepositAsync(Deposit(WiredChestKind.Coins, 20), default);

        _fakes.Handlers["CreditAsync"] = call =>
            call.Args.Length == 4 ? Task.FromResult(WalletCreditResult.Rejected) : Fakes.NotHandled;

        var withdrawal = await chest.WithdrawAsync(
            Withdraw(WiredChestKind.Coins, 4, WiredChestIterationMode.FirstInFirstOut),
            default
        );

        withdrawal.Failure.Should().Be(WiredTransactionFailureType.InternalError);
        withdrawal.Summary.Coins.Should().Be(10);
    }

    private async Task<IWiredChestGrain> ChestAsync()
    {
        var grain = GrainHarness.Create(
            typeof(RoomGrain).Assembly,
            "Turbo.Rooms.Grains.WiredTrading.WiredChestGrain",
            _fakes,
            _db
        );

        RoomHarness.SetMember(
            RoomHarness.GetField(grain, "_state")!,
            "ChestId",
            (RoomObjectId)CHEST
        );
        RoomHarness.SetField(grain, "_defsProvider", _fakes.Create<IFurnitureDefinitionProvider>());
        RoomHarness.SetField(grain, "_furnitureLoader", _loader);

        await ((Orleans.Grain)grain).OnActivateAsync(default);

        return (IWiredChestGrain)grain;
    }

    private IEnumerable<RoomObjectId> Released() =>
        _fakes
            .Log.Of("ReleaseFurnitureAsync")
            .SelectMany(call => (ImmutableArray<RoomObjectId>)call.Args[0]!);

    private List<FurnitureEntity> Rows(params int[] ids)
    {
        using var ctx = _db.CreateDbContext();

        return [.. ctx.Furnitures.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id)];
    }

    private ImmutableArray<FurnitureItemSnapshot> InventorySnapshots(
        ImmutableArray<RoomObjectId> ids
    )
    {
        using var ctx = _db.CreateDbContext();
        var wanted = ids.Select(x => x.Value).ToList();

        return
        [
            .. ctx
                .Furnitures.AsNoTracking()
                .Where(x =>
                    wanted.Contains(x.Id) && x.RoomEntityId == null && x.ChestItemEntityId == null
                )
                .OrderBy(x => x.Id)
                .AsEnumerable()
                .Select(x => new FurnitureItemSnapshot
                {
                    ItemId = x.Id,
                    SpriteId = _definitions[x.FurnitureDefinitionEntityId].SpriteId,
                    OwnerId = x.PlayerEntityId,
                    OwnerName = "alice",
                    Definition = _definitions[x.FurnitureDefinitionEntityId],
                    StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "" },
                    ExtraData = "",
                    SecondsToExpiration = 0,
                    HasRentPeriodStarted = false,
                    RoomId = 0,
                }),
        ];
    }

    private static WiredChestSettingsSnapshot Settings(WiredChestKind kind) =>
        new()
        {
            Kind = kind,
            OwnerId = OWNER,
            RoomId = 1,
            IsStarter = false,
            PreviewMode = WiredChestPreviewMode.None,
            PreviewAmount = 1,
        };

    private static WiredChestDepositRequest Deposit(WiredChestKind kind, params int[] itemIds) =>
        new()
        {
            Chest = Settings(kind),
            DepositorId = ALICE,
            DepositorName = "alice",
            ItemIds = [.. itemIds.Select(RoomObjectId.Parse)],
            Capacity = 1000,
            Type = WiredTransactionType.Manual,
            DefinitionInfo = "",
        };

    private static WiredChestWithdrawRequest Withdraw(
        WiredChestKind kind,
        int amount,
        WiredChestIterationMode order
    ) =>
        new()
        {
            Chest = Settings(kind),
            ReceiverId = ALICE,
            ReceiverName = "alice",
            Amount = amount,
            ItemType = null,
            Order = order,
            Type = WiredTransactionType.Manual,
            DefinitionInfo = "",
        };

    private static FurnitureDefinitionSnapshot Definition(int id, string name) =>
        new()
        {
            Id = id,
            SpriteId = 1000 + id,
            Name = name,
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            LogicName = "default_floor",
            TotalStates = 2,
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
        };

    private static FurnitureDefinitionEntity DefinitionEntity(
        FurnitureDefinitionSnapshot definition
    ) =>
        new()
        {
            Id = definition.Id,
            SpriteId = definition.SpriteId,
            Name = definition.Name,
            ProductType = definition.ProductType,
            FurniCategory = definition.FurniCategory,
            Logic = definition.LogicName,
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
        };

    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };

    private static FurnitureEntity Furni(int id, int definitionId, int ownerId) =>
        new()
        {
            Id = id,
            PlayerEntityId = ownerId,
            FurnitureDefinitionEntityId = definitionId,
        };
}
