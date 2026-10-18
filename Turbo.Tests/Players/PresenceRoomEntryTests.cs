using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Streams;
using Turbo.Players;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

public sealed class PresenceRoomEntryTests
{
    private const int PLAYER = 1;
    private static readonly RoomId ROOM = RoomId.Parse(42);

    [Fact]
    public async Task EnteringAnotherPlayersRoom_CountsTheVisitFromTheOwnerTheRoomNamed()
    {
        // The owner comes with the admission, so the visit is counted although the database
        // holds no row for the room: an entry never reads the room back.
        var fakes = new Fakes();
        var presence = Presence(fakes);

        await presence.SetActiveRoomAsync(ROOM, (PlayerId)2, TestContext.Current.CancellationToken);

        var fact = fakes.Log.Of("Record").Should().ContainSingle().Subject.Args[2];
        fact.Should()
            .BeOfType<AchievementFact>()
            .Which.Source.Should()
            .Be(AchievementSources.VISIT);
        ((AchievementFact)fact!).Value.Should().Be("42");
        fakes.Log.Of("RecordActivityAsync").Should().ContainSingle();
        (await presence.GetActiveRoomAsync(TestContext.Current.CancellationToken))
            .RoomId.Should()
            .Be(ROOM);
    }

    [Fact]
    public async Task EnteringOwnRoom_CountsNoVisit()
    {
        var fakes = new Fakes();
        var presence = Presence(fakes);

        await presence.SetActiveRoomAsync(
            ROOM,
            (PlayerId)PLAYER,
            TestContext.Current.CancellationToken
        );

        fakes.Log.Of("Record").Should().BeEmpty();
        fakes.Log.Of("RecordActivityAsync").Should().BeEmpty();
        fakes.Log.Of("AddPlayerToRoomAsync").Should().ContainSingle();
    }

    [Fact]
    public async Task AFailedVisitWrite_StillLetsThePlayerIntoTheRoom()
    {
        var fakes = new Fakes();
        fakes.Handlers["Record"] = _ => throw new InvalidOperationException("database down");
        var presence = Presence(fakes);

        await presence.SetActiveRoomAsync(ROOM, (PlayerId)2, TestContext.Current.CancellationToken);

        (await presence.GetActiveRoomAsync(TestContext.Current.CancellationToken))
            .RoomId.Should()
            .Be(ROOM);
        fakes.Log.Of("AddPlayerToRoomAsync").Should().ContainSingle();
        fakes.Log.Of("CreateAvatarFromPlayerAsync").Should().ContainSingle();
        fakes.Log.Of("RecordActivityAsync").Should().BeEmpty();
    }

    private static IPlayerPresenceGrain Presence(Fakes fakes)
    {
        // The room stream comes from a keyed provider; the fakes' own service provider has no keys.
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

        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.PlayerPresenceGrain",
            fakes,
            playerId: PLAYER
        );

        // The harness never ran the field initializers: give the grain's own collections (the
        // outbound holds) and its state's a value, as SessionHarness does for the state.
        var state = grain.GetType().GetField("_state", ALL)!.GetValue(grain)!;
        foreach (var (owner, field) in Collections(grain).Concat(Collections(state)))
            field.SetValue(owner, Activator.CreateInstance(field.FieldType));

        return (IPlayerPresenceGrain)grain;
    }

    private const System.Reflection.BindingFlags ALL =
        System.Reflection.BindingFlags.Instance
        | System.Reflection.BindingFlags.Public
        | System.Reflection.BindingFlags.NonPublic;

    private static IEnumerable<(object, System.Reflection.FieldInfo)> Collections(object owner) =>
        owner
            .GetType()
            .GetFields(ALL)
            .Where(f =>
                f.GetValue(owner) is null
                && f.FieldType.IsClass
                && f.FieldType.Namespace == "System.Collections.Generic"
                && f.FieldType.GetConstructor(Type.EmptyTypes) is not null
            )
            .Select(f => (owner, f));
}
