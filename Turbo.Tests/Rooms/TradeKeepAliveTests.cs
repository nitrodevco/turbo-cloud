using FluentAssertions;
using Orleans.Runtime;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A trade lives only in the room's trade grain, which nothing calls while two players look at
/// their offers. Orleans collects an idle activation (GrainCollectionAgeMinutes) while both stay
/// online, and the collected grain closes every trade it held; an open trade keeps it loaded.
/// </summary>
public sealed class TradeKeepAliveTests
{
    private const int ROOM = 5;
    private const int INITIATOR = 1;
    private const int PARTNER = 2;

    /// <summary>RoomConfig.TradeKeepAliveMinutes as shipped.</summary>
    private static readonly TimeSpan SHIPPED_KEEP_ALIVE = TimeSpan.FromMinutes(30);

    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public TradeKeepAliveTests()
    {
        _fakes.Handlers[nameof(IRoomGrain.GetTradePartiesAsync)] = _ =>
            Task.FromResult<TradePartiesSnapshot?>(
                new TradePartiesSnapshot
                {
                    InitiatorId = INITIATOR,
                    PartnerId = PARTNER,
                    PartnerName = "partner",
                    InitiatorMayTrade = true,
                    PartnerMayTrade = true,
                }
            );
        _fakes.Handlers["HasAsync"] = _ => Task.FromResult(true);
        // Both own the trading pass badge.
        _fakes.Handlers["HasBadgeAsync"] = _ => Task.FromResult(true);
    }

    [Fact]
    public async Task AnOpenTrade_KeepsTheTradeGrainLoaded()
    {
        var trades = NewTradeGrain();

        (await trades.OpenAsync(ActionContext.CreateForPlayer(INITIATOR, ROOM), 2, Ct))
            .Should()
            .BeTrue();

        Pins()
            .Should()
            .ContainSingle("idle collection would close the trade")
            .Which.Should()
            .Be(SHIPPED_KEEP_ALIVE);
    }

    [Fact]
    public async Task EachTradeAction_KeepsItLoadedAgain()
    {
        var trades = NewTradeGrain();
        var initiator = ActionContext.CreateForPlayer(INITIATOR, ROOM);

        await trades.OpenAsync(initiator, 2, Ct);
        await trades.AcceptAsync(initiator, true, Ct);

        Pins().Should().HaveCount(2, "the time counts from the last action");
    }

    [Fact]
    public async Task NoTrade_LeavesCollectionAlone()
    {
        var trades = NewTradeGrain();

        await trades.CloseAsync(ActionContext.CreateForPlayer(INITIATOR, ROOM), Ct);

        Pins().Should().BeEmpty();
    }

    private IEnumerable<TimeSpan> Pins() =>
        _fakes
            .Log.Of(nameof(IGrainRuntime.DelayDeactivation))
            .Select(call => (TimeSpan)call.Args[1]!);

    private IRoomTradeGrain NewTradeGrain()
    {
        var grain = GrainHarness.Create<RoomTradeGrain>(_fakes);
        GrainHarness.Replace(grain, new RoomTradeLiveState { RoomId = ROOM });

        return (IRoomTradeGrain)grain;
    }
}
