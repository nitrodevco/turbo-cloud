using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Players;
using Turbo.Database.Extensions;
using Turbo.Furniture.Providers;
using Turbo.Inventory.Factories;
using Turbo.Inventory.Grains;
using Turbo.Inventory.Grains.Modules;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>
/// A client that has asked for its furni list is sent each item that joins it; one that has not
/// is sent no list packet at all, since it asks for the whole list when the inventory opens.
/// The list belongs to the session that asked: the next login starts without one.
/// </summary>
public sealed class FurniListUpdateTests : IDisposable
{
    private const int PLAYER = 7;
    private const int CHAIR = 1;

    private static readonly SessionKey Session = new("session-1");

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly FurnitureDefinitionEntity _chair = new()
    {
        Id = CHAIR,
        SpriteId = CHAIR,
        Name = "chair",
        ProductType = ProductType.Floor,
        FurniCategory = FurnitureCategory.Default,
        Logic = "default_floor",
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

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public FurniListUpdateTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "player",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(_chair);

        _fakes.Handlers["TryGetDefinition"] = call =>
            (int)call.Args[0]! == CHAIR ? _chair.ToSnapshot(0.01) : null;
        _fakes.Handlers["GetPlayerNameAsync"] = _ => Task.FromResult<string?>("player");
        _fakes.Handlers[nameof(IPlayerPresenceGrain.TrySendComposerToSessionAsync)] = call =>
            Task.FromResult((SessionKey)call.Args[0]! == Session);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task APurchase_BeforeTheInventoryWasOpened_SendsNoListPacket()
    {
        var granted = await NewInventory()
            .GrantFurnitureAsync(CHAIR, null, TestCustody.Cause(), Ct);

        granted.Should().NotBeNull();
        ListPackets()
            .OfType<FurniListAddOrUpdateEventMessageComposer>()
            .Should()
            .BeEmpty("the client asks for the whole list when it opens");
        _fakes.Log.Of("AddAsync").Should().ContainSingle("the item is still marked new");
    }

    [Fact]
    public async Task APurchase_AfterTheInventoryGrainWasCollected_CallsTheListStaleOnce()
    {
        await NewInventory().SendFurnitureInventoryAsync(Session, Ct);

        // Collected while the player stayed online: the next call activates a new grain, which
        // does not know the client still holds the list it was sent.
        var activatedAgain = NewInventory();
        await activatedAgain.GrantFurnitureAsync(CHAIR, null, TestCustody.Cause(), Ct);
        await activatedAgain.GrantFurnitureAsync(CHAIR, null, TestCustody.Cause(), Ct);

        ListPackets()
            .Should()
            .ContainSingle("the client is told once that the list it holds lacks items")
            .Which.Should()
            .BeOfType<FurniListInvalidateEventMessageComposer>();
    }

    [Fact]
    public async Task APurchase_AfterTheInventoryWasOpened_SendsTheItem()
    {
        var inventory = NewInventory();

        await inventory.SendFurnitureInventoryAsync(Session, Ct);
        var granted = await inventory.GrantFurnitureAsync(CHAIR, null, TestCustody.Cause(), Ct);

        var sent = _fakes
            .Log.Of(nameof(IPlayerPresenceGrain.TrySendComposerToSessionAsync))
            .Should()
            .ContainSingle()
            .Subject;

        sent.Args[0].Should().Be(Session);
        sent.Args[1]
            .Should()
            .BeOfType<FurniListAddOrUpdateEventMessageComposer>()
            .Which.Items.Should()
            .ContainSingle()
            .Which.ItemId.Should()
            .Be(granted!.ItemId);
        ListPackets()
            .OfType<FurniListInvalidateEventMessageComposer>()
            .Should()
            .BeEmpty("the client is not made to fetch the list again");
    }

    [Fact]
    public async Task AFurtherLogin_HasNoListUntilItAsks()
    {
        var inventory = NewInventory();

        await inventory.SendFurnitureInventoryAsync(new SessionKey("earlier-session"), Ct);
        await inventory.GrantFurnitureAsync(CHAIR, null, TestCustody.Cause(), Ct);
        await inventory.GrantFurnitureAsync(CHAIR, null, TestCustody.Cause(), Ct);

        _fakes
            .Log.Of(nameof(IPlayerPresenceGrain.TrySendComposerToSessionAsync))
            .Should()
            .ContainSingle("once the session that asked has gone, no later one is sent items");
    }

    /// <summary>Every furni list packet the player was sent, however it was sent.</summary>
    private IEnumerable<object?> ListPackets() =>
        _fakes
            .Log.Calls.Where(x =>
                x.Method
                    is "SendComposerAsync"
                        or "TrySendComposerAsync"
                        or nameof(IPlayerPresenceGrain.TrySendComposerToSessionAsync)
            )
            .SelectMany(x => x.Args)
            .SelectMany(arg =>
                arg is IEnumerable<IComposer?> many ? many : new[] { arg as IComposer }
            )
            .Where(arg =>
                arg
                    is FurniListAddOrUpdateEventMessageComposer
                        or FurniListInvalidateEventMessageComposer
            );

    /// <summary>The player's inventory grain with its real furniture section over this database.</summary>
    private IInventoryGrain NewInventory()
    {
        var grain = GrainHarness.Create<InventoryGrain>(_fakes, _db, PLAYER);
        var definitions = _fakes.Create<IFurnitureDefinitionProvider>();
        var loader = new InventoryFurnitureLoader(
            _db,
            definitions,
            new StuffDataFactory(NullLogger<IStuffDataFactory>.Instance),
            NullLogger<IInventoryFurnitureLoader>.Instance
        );
        var module = new InventoryFurniModule(
            grain,
            GrainHarness.Get<InventoryLiveState>(grain),
            TestCustody.Over(_db),
            loader,
            definitions,
            _fakes.Create<Turbo.Primitives.Catalog.ICatalogService>(),
            NullLogger.Instance
        );

        GrainHarness.Replace<InventoryFurniModule>(grain, module);

        return (IInventoryGrain)grain;
    }
}
