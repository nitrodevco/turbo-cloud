using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Catalog;
using Turbo.Catalog.Exceptions;
using Turbo.Events;
using Turbo.Players.Policies;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Action;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Events;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Policies;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Catalog;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Plugins;

/// <summary>
/// Game policies a plugin brings (<c>docs/plugin-policies.md</c>): a public class implementing
/// <see cref="IPurchasePolicy"/>, <see cref="IRoomEntryPolicy"/> or <see cref="IPlacementPolicy"/>
/// is found as its plugin loads and decides from then on, at the entry point the player's action
/// takes; two plugins deciding one kind both run in order and are reported as a conflict; and the
/// post-commit events go out once for what was done, never for what a policy refused.
/// </summary>
public sealed class GamePolicyTests : IAsyncDisposable
{
    private const int PLAYER = 7;
    private const int OPEN_ROOM = 20;
    private const int CLOSED_ROOM = 21;

    private readonly CatalogFixture _catalog = new();
    private readonly CapturingLogger<GamePolicies> _log = new();
    private readonly GamePolicies _policies;
    private readonly TestEventBus _events = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-game-policies-" + Guid.NewGuid().ToString("N")
    );
    private PluginManager? _manager;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GamePolicyTests()
    {
        _policies = new GamePolicies(_log);
        _events.Record<PurchaseCompletedEvent>();
        _events.Record<RoomEnteredEvent>();
    }

    public async ValueTask DisposeAsync()
    {
        if (_manager is { } manager)
            await manager.UnloadAllAsync(CancellationToken.None);

        _catalog.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private const string USINGS = """
        using System.Threading;
        using System.Threading.Tasks;
        using Turbo.Primitives.Policies;

        """;

    [Fact]
    public async Task A_plugins_purchase_policy_sets_what_the_buyer_pays_and_the_event_says_so()
    {
        await LoadAsync(
            (
                "halfprice",
                """
                public sealed class HalfPrice : IPurchasePolicy
                {
                    public ValueTask<PurchaseDecision> DecideAsync(
                        PurchaseRequest request, PurchaseDecision proposed, CancellationToken ct
                    ) => ValueTask.FromResult(proposed with { CostCredits = proposed.CostCredits / 2 });
                }
                """
            )
        );

        await BuyAsync(SOLD, 2);

        // The offer is 5 credits; two would be 10.
        Charged().Should().Be(5);
        _catalog.Fakes.Log.Of("GrantCatalogOfferAsync").Should().ContainSingle();

        await _events.WhenPublishedAsync<PurchaseCompletedEvent>(1);
        var completed = _events.Of<PurchaseCompletedEvent>().Single();

        completed.PlayerId.Value.Should().Be(PLAYER);
        completed.OfferId.Should().Be(SOLD);
        completed.CostCredits.Should().Be(5);
        completed.Units.Should().Be(2);
    }

    [Fact]
    public async Task A_purchase_a_policy_refuses_charges_nothing_tells_its_reason_and_raises_no_event()
    {
        await LoadAsync(
            (
                "membersonly",
                """
                public sealed class MembersOnly : IPurchasePolicy
                {
                    public ValueTask<PurchaseDecision> DecideAsync(
                        PurchaseRequest request, PurchaseDecision proposed, CancellationToken ct
                    ) => ValueTask.FromResult(PurchaseDecision.Refuse("Members only."));
                }
                """
            )
        );

        var buy = () => BuyAsync(SOLD, 1);

        (await buy.Should().ThrowAsync<CatalogPurchaseException>())
            .Which.Refusal.Should()
            .Be("Members only.");
        _catalog.Fakes.Log.Of("TryDebitAsync").Should().BeEmpty();
        _catalog.Fakes.Log.Of("GrantCatalogOfferAsync").Should().BeEmpty();

        await _events.WhenPublishedAsync<PurchaseCompletedEvent>(0);
        _events.Of<PurchaseCompletedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task Without_a_policy_a_purchase_costs_what_the_offer_says()
    {
        await BuyAsync(SOLD, 2);

        Charged().Should().Be(10);
    }

    [Fact]
    public async Task A_plugins_room_entry_policy_refuses_one_room_and_lets_the_other_be_entered_once()
    {
        await LoadAsync(
            (
                "closedroom",
                $$"""
                public sealed class ClosedRoom : IRoomEntryPolicy
                {
                    public ValueTask<RoomEntryDecision> DecideAsync(RoomEntryRequest request, CancellationToken ct) =>
                        ValueTask.FromResult(
                            request.RoomId.Value == {{CLOSED_ROOM}}
                                ? RoomEntryDecision.Refuse("Closed for the party.")
                                : RoomEntryDecision.Allow
                        );
                }
                """
            )
        );
        var fakes = EntryFakes();
        var rooms = Rooms(fakes);

        await rooms.OpenRoomForPlayerIdAsync(
            ActionContext.CreateForPlayer(PLAYER, -1),
            PLAYER,
            CLOSED_ROOM,
            RoomEntryAccessType.Allowed,
            Ct
        );

        fakes
            .Sent<NotificationDialogMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Parameters["message"]
            .Should()
            .Be("Closed for the party.");
        fakes
            .Sent<CantConnectMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.ErrorType.Should()
            .Be(RoomConnectionErrorType.NoEntry);
        fakes.Log.Of("GetEntryViewAsync").Should().BeEmpty("the room is not streamed");

        await rooms.OpenRoomForPlayerIdAsync(
            ActionContext.CreateForPlayer(PLAYER, -1),
            PLAYER,
            OPEN_ROOM,
            RoomEntryAccessType.Allowed,
            Ct
        );

        fakes.Log.Of("GetEntryViewAsync").Should().ContainSingle();
        await _events.WhenPublishedAsync<RoomEnteredEvent>(1);
        _events
            .Of<RoomEnteredEvent>()
            .Should()
            .ContainSingle()
            .Which.RoomId.Value.Should()
            .Be(OPEN_ROOM);
    }

    [Fact]
    public async Task A_room_entry_policy_can_send_the_player_to_another_room_instead()
    {
        await LoadAsync(
            (
                "lobbyfirst",
                $$"""
                public sealed class LobbyFirst : IRoomEntryPolicy
                {
                    public ValueTask<RoomEntryDecision> DecideAsync(RoomEntryRequest request, CancellationToken ct) =>
                        ValueTask.FromResult(
                            request.RoomId.Value == {{CLOSED_ROOM}}
                                ? RoomEntryDecision.Redirect({{OPEN_ROOM}})
                                : RoomEntryDecision.Allow
                        );
                }
                """
            )
        );
        var fakes = EntryFakes();

        await Rooms(fakes)
            .OpenRoomForPlayerIdAsync(
                ActionContext.CreateForPlayer(PLAYER, -1),
                PLAYER,
                CLOSED_ROOM,
                RoomEntryAccessType.Allowed,
                Ct
            );

        fakes.Sent<CloseConnectionMessageComposer>().Should().ContainSingle();
        fakes.Sent<CantConnectMessageComposer>().Should().BeEmpty();
        fakes
            .Log.Of("ForwardToRoomAsync")
            .Should()
            .ContainSingle()
            .Which.Args[0]
            .Should()
            .Be((RoomId)OPEN_ROOM);
        fakes.Log.Of("GetEntryViewAsync").Should().BeEmpty();
        await _events.WhenPublishedAsync<RoomEnteredEvent>(0);
    }

    [Fact]
    public async Task A_plugins_placement_policy_keeps_a_furni_and_a_floor_plan_out_with_its_reason()
    {
        await LoadAsync(
            (
                "nochairs",
                $$"""
                public sealed class NoChairs : IPlacementPolicy
                {
                    public ValueTask<PlacementDecision> DecideAsync(PlacementRequest request, CancellationToken ct) =>
                        ValueTask.FromResult(
                            request.Definition?.Id == {{CHAIR}} || request.Kind == PlacementKind.FloorPlan
                                ? PlacementDecision.Refuse("Not in this hotel.")
                                : PlacementDecision.Allow
                        );
                }
                """
            )
        );
        var fakes = _catalog.Fakes;
        var chair = _catalog.Definitions.TryGetDefinition(CHAIR)!;

        fakes.Handlers["GetItemSnapshotAsync"] = _ =>
            Task.FromResult<FurnitureItemSnapshot?>(
                new FurnitureItemSnapshot
                {
                    ItemId = 50,
                    SpriteId = chair.SpriteId,
                    OwnerId = PLAYER,
                    OwnerName = "buyer",
                    Definition = chair,
                    StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "0" },
                    ExtraData = string.Empty,
                    SecondsToExpiration = -1,
                    HasRentPeriodStarted = false,
                    RoomId = -1,
                }
            );
        var rooms = Rooms(fakes);
        var ctx = ActionContext.CreateForPlayer(PLAYER, OPEN_ROOM);

        await rooms.PlaceFloorItemInRoomAsync(ctx, 50, 1, 1, Rotation.North, Ct);
        await rooms.SaveFloorPlanAsync(ctx, "xxx\rx0x", null, Ct);

        fakes.Log.Of("PlaceFloorItemAsync").Should().BeEmpty("the room is not asked");
        fakes.Log.Of("SaveFloorPlanAsync").Should().BeEmpty();
        fakes
            .SentToPlayer<NotificationDialogMessageComposer>(PLAYER)
            .Select(x => (x.NotificationType, x.Parameters.Values.Single(v => v != "POP_UP")))
            .Should()
            .Equal(
                (PolicyRefusals.PLACEMENT, "Not in this hotel."),
                (RoomGrain.FLOOR_PLAN_ERROR_NOTIFICATION, "Not in this hotel.")
            );
    }

    [Fact]
    public async Task Two_plugins_with_a_purchase_policy_both_decide_in_order_and_are_reported_as_a_conflict()
    {
        var snapshots = await LoadAsync(
            (
                "alpha",
                """
                public sealed class Discount : IPurchasePolicy
                {
                    public ValueTask<PurchaseDecision> DecideAsync(
                        PurchaseRequest request, PurchaseDecision proposed, CancellationToken ct
                    ) => ValueTask.FromResult(proposed with { CostCredits = proposed.CostCredits - 4 });
                }
                """
            ),
            (
                "beta",
                """
                public sealed class Double : IPurchasePolicy
                {
                    public ValueTask<PurchaseDecision> DecideAsync(
                        PurchaseRequest request, PurchaseDecision proposed, CancellationToken ct
                    ) => ValueTask.FromResult(proposed with { CostCredits = proposed.CostCredits * 2 });
                }
                """
            )
        );

        await BuyAsync(SOLD, 2);

        // Plugin key order: alpha's 10 - 4, then beta's doubling.
        Charged().Should().Be(12);
        snapshots
            .Select(x => (x.Key, x.Policies.Single().SharedWith.Single()))
            .Should()
            .BeEquivalentTo([("alpha", "beta"), ("beta", "alpha")]);
        _log.AtLeast(LogLevel.Warning)
            .Should()
            .Contain(x => x.Message.Contains("Conflict") && x.Message.Contains("purchase"));

        await _manager!.UnloadAllAsync(Ct);

        _policies.Registered.Should().BeEmpty("a plugin's policies go with it");
    }

    private async Task<ImmutableArray<PluginSnapshot>> LoadAsync(
        params (string Key, string Source)[] plugins
    )
    {
        foreach (var (key, source) in plugins)
            PluginBuilder.Write(_folder, key, USINGS + $"namespace {key};\n\n" + source);

        _manager = new PluginManager(
            new ServiceCollection().AddSingleton<IGamePolicies>(_policies).BuildServiceProvider(),
            new AssemblyProcessor([new GamePolicyFeatureProcessor(_policies)]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            NullLogger<PluginManager>.Instance
        );

        await _manager.LoadAllAsync(ct: Ct);

        var snapshots = _manager.Snapshot();

        snapshots.Should().OnlyContain(x => x.State == PluginStateType.Loaded, "{0}", snapshots);

        return snapshots;
    }

    private int Charged() =>
        (
            (IEnumerable<WalletDebitRequest>)
                _catalog.Fakes.Log.Of("TryDebitAsync").Single().Args[0]!
        ).Sum(x => x.Amount);

    /// <summary>The buyer's purchase grain buying an offer from the catalog as published.</summary>
    private async Task BuyAsync(int offerId, int quantity)
    {
        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);

        var fakes = _catalog.Fakes;

        fakes.Handlers["GetCatalogSnapshot"] = _ => provider.Current;
        fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());

        var grain = GrainHarness.Create(
            typeof(CatalogModule).Assembly,
            "Turbo.Catalog.Grains.CatalogPurchaseGrain",
            fakes,
            _catalog.Db,
            playerId: PLAYER
        );

        Set(grain, "_catalogService", fakes.Create<ICatalogService>());
        Set(grain, "_definitionProvider", _catalog.Definitions);
        GrainHarness.Replace<IGamePolicies>(grain, _policies);
        GrainHarness.Replace(grain, _events.System);
        typeof(Grain)
            .GetProperty(
                "GrainContext",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            )!
            .GetSetMethod(true)!
            .Invoke(
                grain,
                [
                    GrainContextStub.Create(
                        GrainId.Create(
                            GrainType.Create("catalogpurchase"),
                            GrainIdKeyExtensions.CreateIntegerKey(PLAYER)
                        )
                    ),
                ]
            );

        await ((ICatalogPurchaseGrain)grain).PurchaseOfferFromCatalogAsync(
            CatalogType.Normal,
            offerId,
            string.Empty,
            quantity,
            Ct
        );
    }

    private IRoomService Rooms(Fakes fakes) =>
        new RoomService(
            NullLogger<IRoomService>.Instance,
            Options.Create(new RoomConfig()),
            fakes.Create<IGrainFactory>(),
            TimeProvider.System,
            _policies,
            _events.System
        );

    /// <summary>A grain factory whose rooms stream an empty room to whoever enters.</summary>
    private static Fakes EntryFakes()
    {
        var fakes = new Fakes();

        fakes.Handlers["GetPendingRoomAsync"] = _ =>
            Task.FromResult(new RoomPendingSnapshot { RoomId = -1, State = RoomEntryState.None });
        fakes.Handlers["GetEntryViewAsync"] = _ =>
            Task.FromResult(
                new RoomEntryViewSnapshot
                {
                    Room = Uninitialized<RoomSnapshot>() with { WorldType = string.Empty },
                    Map = Uninitialized<RoomMapSnapshot>() with
                    {
                        ModelData = string.Empty,
                        TileEncodedHeights = [],
                    },
                    OwnerNames = ImmutableDictionary<
                        Turbo.Primitives.Players.PlayerId,
                        string
                    >.Empty,
                    FloorItems = [],
                    WallItems = [],
                    Avatars = [],
                    Properties = [],
                    AreaHides = [],
                }
            );

        return fakes;
    }

    private static T Uninitialized<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private static void Set(object grain, string field, object value) =>
        grain
            .GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grain, value);
}
