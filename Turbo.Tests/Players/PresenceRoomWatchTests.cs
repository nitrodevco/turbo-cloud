using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Streams;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Watching;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// A player watching another room for a plugin (docs/plugin-room-watch.md): the presence takes
/// the room's stream beside their own room's, and each of its packets reaches the session inside
/// the plugin's envelope, in order with everything else they are sent, the same instance every
/// player in that room gets (written once). Only a room they could walk into, a limited number,
/// and until the lease runs out, the session ends or they walk in.
/// </summary>
public sealed class PresenceRoomWatchTests
{
    private const string PLUGIN = "portals";
    private static readonly RoomId WATCHED = RoomId.Parse(9);

    private readonly SessionHarness _hotel = new();
    private readonly PluginMessageComposer _envelope = new()
    {
        Name = "portals.room",
        Body = [0, 0, 0, 9],
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PresenceRoomWatchTests()
    {
        // The room stream comes from a keyed provider; the fakes' own service provider has no keys.
        var fakes = _hotel.Fakes;
        var services = new ServiceCollection()
            .AddKeyedSingleton(
                OrleansStreamProviders.ROOM_STREAM_PROVIDER,
                fakes.Create<IStreamProvider>()
            )
            .BuildServiceProvider();
        fakes.Handlers["get_ActivationServices"] = _ => services;
        fakes.Handlers["GetStream"] = _ => fakes.Create<IAsyncStream<RoomOutboundSnapshot>>();
        fakes.Handlers["GetAllSubscriptionHandles"] = _ =>
            Task.FromResult<IList<StreamSubscriptionHandle<RoomOutboundSnapshot>>>([]);
        fakes.Handlers["SubscribeAsync"] = _ =>
            Task.FromResult<StreamSubscriptionHandle<RoomOutboundSnapshot>>(null!);
    }

    [Fact]
    public Task A_watched_rooms_packets_reach_the_player_in_the_envelope_in_order_with_their_own() =>
        SessionHarness.InTurns(async () =>
        {
            var key = await LogInAsync(clientMessages: true);
            var walk = Core();
            var chat = Core();
            var direct = Core();
            var later = Core();

            (await WatchAsync()).Should().Be(RoomWatchOutcome.Watching);

            await PublishAsync(WATCHED, walk, chat);
            await _hotel.PresenceGrain.SendComposerAsync(direct, Ct);
            await PublishAsync(WATCHED, later);
            await _hotel.WhenPresenceIdleAsync();

            var received = _hotel.Received(key);

            received.Should().HaveCount(4);
            received[2].Should().BeSameAs(direct);
            received
                .Where((_, i) => i != 2)
                .Should()
                .AllSatisfy(x =>
                    x.Should().BeOfType<PluginEnvelopeComposer>().Which.Outer.Should().Be(_envelope)
                );
            received
                .OfType<PluginEnvelopeComposer>()
                .Select(x => x.Inner)
                .Should()
                .Equal([walk, chat, later], ReferenceEquals, "the room's own instances");
        });

    [Fact]
    public Task A_session_that_did_not_accept_plugin_messages_gets_nothing_of_the_watched_room() =>
        SessionHarness.InTurns(async () =>
        {
            var key = await LogInAsync(clientMessages: false);

            await WatchAsync();
            await PublishAsync(WATCHED, Core());
            await _hotel.WhenPresenceIdleAsync();

            _hotel.Received(key).Should().BeEmpty();
        });

    [Fact]
    public Task A_room_the_player_could_not_walk_into_is_not_watched_unless_through_a_linked_pair() =>
        SessionHarness.InTurns(async () =>
        {
            await LogInAsync(clientMessages: true);

            // A doorbell room: the door lets in only who passes it (a linked pair into it).
            _hotel.Fakes.Handlers[nameof(IRoomGrain.CheckEntryAccessAsync)] = call =>
                Task.FromResult(
                    (bool)call.Args[2]! ? RoomEntryAccessType.Allowed : RoomEntryAccessType.Doorbell
                );

            (await WatchAsync()).Should().Be(RoomWatchOutcome.DoorClosed);
            HoldsTaken().Should().Be(0);

            (await WatchAsync(throughLinkedPair: true)).Should().Be(RoomWatchOutcome.Watching);
            HoldsTaken().Should().Be(1);

            // A ban placed since ends it at the next renewal; a full room never kept a watcher out.
            _hotel.Fakes.Handlers[nameof(IRoomGrain.CheckEntryAccessAsync)] = _ =>
                Task.FromResult(RoomEntryAccessType.Banned);
            (await WatchAsync(throughLinkedPair: true)).Should().Be(RoomWatchOutcome.Banned);
            await AssertNotWatchingAsync();

            _hotel.Fakes.Handlers[nameof(IRoomGrain.CheckEntryAccessAsync)] = _ =>
                Task.FromResult(RoomEntryAccessType.Full);
            (await WatchAsync()).Should().Be(RoomWatchOutcome.Watching);
        });

    [Fact]
    public Task A_player_watches_a_few_rooms_for_one_plugin_each_and_never_their_own() =>
        SessionHarness.InTurns(async () =>
        {
            (await WatchAsync()).Should().Be(RoomWatchOutcome.NotOnline);

            await LogInAsync(clientMessages: true);

            for (var i = 0; i < RoomWatchLimits.MAX_WATCHES_PER_PLAYER; i++)
                (await WatchAsync(RoomId.Parse(20 + i))).Should().Be(RoomWatchOutcome.Watching);

            (await WatchAsync(RoomId.Parse(30))).Should().Be(RoomWatchOutcome.TooManyWatches);
            (await WatchAsync(RoomId.Parse(20)))
                .Should()
                .Be(RoomWatchOutcome.Watching, "a renewal");
            (await WatchAsync(RoomId.Parse(21), plugin: "radio"))
                .Should()
                .Be(RoomWatchOutcome.WatchedForAnotherPlugin);

            await _hotel.PresenceGrain.SetActiveRoomAsync(RoomId.Parse(40), (PlayerId)2, Ct);
            (await WatchAsync(RoomId.Parse(40))).Should().Be(RoomWatchOutcome.InThatRoom);
        });

    [Fact]
    public Task A_watch_ends_when_its_lease_runs_out_and_lets_the_room_go() =>
        SessionHarness.InTurns(async () =>
        {
            await LogInAsync(clientMessages: true);
            await WatchAsync();

            await _hotel.Presence.EndExpiredWatchesAsync(DateTime.UtcNow.AddSeconds(30));
            await AssertWatchingAsync();

            await _hotel.Presence.EndExpiredWatchesAsync(DateTime.UtcNow.AddSeconds(61));
            await AssertNotWatchingAsync();
            HoldsReleased()
                .Should()
                .Equal((PLUGIN, string.Empty, (PlayerId)SessionHarness.PlayerId));
        });

    [Fact]
    public Task A_watch_ends_with_the_session_and_on_unwatch_by_its_own_plugin_only() =>
        SessionHarness.InTurns(async () =>
        {
            var key = await LogInAsync(clientMessages: true);
            await WatchAsync();

            (await _hotel.PresenceGrain.UnwatchRoomAsync(WATCHED, "radio", Ct)).Should().BeFalse();
            (await _hotel.PresenceGrain.UnwatchRoomAsync(WATCHED, PLUGIN, Ct)).Should().BeTrue();
            await AssertNotWatchingAsync();

            await WatchAsync();
            await _hotel.Gateway.RemoveSessionAsync(key, Ct);
            await AssertNotWatchingAsync(relogin: true);
        });

    [Fact]
    public Task Walking_into_the_watched_room_ends_the_watch_and_its_packets_arrive_as_anyone_elses() =>
        SessionHarness.InTurns(async () =>
        {
            var key = await LogInAsync(clientMessages: true);
            await WatchAsync();

            await _hotel.PresenceGrain.SetActiveRoomAsync(WATCHED, (PlayerId)2, Ct);
            var inside = Core();
            await PublishAsync(WATCHED, inside);
            await _hotel.WhenPresenceIdleAsync();

            _hotel.Received(key).OfType<PluginEnvelopeComposer>().Should().BeEmpty();
            _hotel.Received(key).Should().Contain(inside);
        });

    private async Task<SessionKey> LogInAsync(bool clientMessages)
    {
        var session = _hotel.NewSession(_sessions++);
        var key = session.SessionKey;

        await _hotel.Gateway.AddSessionAsync(key, session);
        await _hotel.Gateway.AddSessionToPlayerAsync(key, SessionHarness.PlayerId);

        if (clientMessages)
            await _hotel.PresenceGrain.SetClientCapabilitiesAsync(
                [
                    new ClientCapabilitySnapshot
                    {
                        Name = ClientCapabilities.CLIENT_MESSAGES,
                        Version = 1,
                    },
                ],
                Ct
            );

        return key;
    }

    private int _sessions = 1;

    private Task<RoomWatchOutcome> WatchAsync(
        RoomId? room = null,
        string plugin = PLUGIN,
        bool throughLinkedPair = false
    ) =>
        _hotel.PresenceGrain.WatchRoomAsync(
            new RoomWatchRequest
            {
                RoomId = room ?? WATCHED,
                Plugin = plugin,
                Envelope = _envelope,
                LeaseMs = 60_000,
                ThroughLinkedPair = throughLinkedPair,
            },
            Ct
        );

    private Task PublishAsync(RoomId room, params IComposer[] composers) =>
        ((IAsyncObserver<RoomOutboundSnapshot>)_hotel.Presence).OnNextAsync(
            new RoomOutboundSnapshot { RoomId = room, Composers = [.. composers] }
        );

    // Watching shows as the room's packets arriving in the envelope.
    private async Task AssertWatchingAsync() => (await DeliveredAsync()).Should().BeTrue();

    private async Task AssertNotWatchingAsync(bool relogin = false)
    {
        if (relogin)
            await LogInAsync(clientMessages: true);

        (await DeliveredAsync()).Should().BeFalse();
    }

    private async Task<bool> DeliveredAsync()
    {
        var probe = Core();

        await PublishAsync(WATCHED, probe);
        await _hotel.WhenPresenceIdleAsync();

        return _hotel
            .Fakes.Log.Calls.Where(c => c.Method is "SendComposersAsync")
            .SelectMany(c => c.Args.OfType<IEnumerable<IComposer>>().SelectMany(x => x))
            .OfType<PluginEnvelopeComposer>()
            .Any(x => ReferenceEquals(x.Inner, probe));
    }

    private int HoldsTaken() => _hotel.Fakes.Log.Of(nameof(IRoomGrain.HoldAsync)).Count();

    private IEnumerable<(string, string, PlayerId)> HoldsReleased() =>
        _hotel
            .Fakes.Log.Of(nameof(IRoomGrain.ReleaseHoldAsync))
            .Select(x => ((string)x.Args[0]!, (string)x.Args[1]!, (PlayerId)x.Args[2]!));

    private static IComposer Core() => new TurboServerCapabilitiesMessage { Capabilities = [] };
}
