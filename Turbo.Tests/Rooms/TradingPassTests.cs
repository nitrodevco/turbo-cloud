using FluentAssertions;
using Orleans;
using Turbo.Players.Configuration;
using Turbo.Players.Trading;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Inventory.Trading;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Trading;
using Turbo.Primitives.Trading.Enums;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Who may open a trade. Habbo asks both players for the trading pass: one starting a trade
/// without it gets the <c>trade.trading_perk</c> notification ("You're not a Habbo Citizen!"),
/// and one asking a player without it gets <c>inventory.trading.openfail.4</c> ("... doesn't want
/// to / cannot trade atm."), the partner hearing nothing (official client capture 2026-10-17 and
/// 2026-10-22, both sides). A hotel can let such players receive only, which the client draws
/// from <c>TradingOpen</c>'s can-trade flags, or ask for no pass.
/// </summary>
public sealed class TradingPassTests
{
    private const int ROOM = 5;
    private const int INITIATOR = 1;
    private const int PARTNER = 2;

    private readonly Fakes _fakes = new();
    private readonly HashSet<long> _passHolders = [INITIATOR, PARTNER];
    private readonly HashSet<long> _tradeLocked = [];
    private bool _roomAllows = true;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public TradingPassTests()
    {
        _fakes.Handlers[nameof(IRoomGrain.GetTradePartiesAsync)] = _ =>
            Task.FromResult<TradePartiesSnapshot?>(
                new TradePartiesSnapshot
                {
                    InitiatorId = INITIATOR,
                    PartnerId = PARTNER,
                    PartnerName = "partner",
                    InitiatorMayTrade = _roomAllows,
                    PartnerMayTrade = _roomAllows,
                }
            );
        _fakes.Handlers["HasAsync"] = call =>
            Task.FromResult(!_tradeLocked.Contains((long)call.Key!));
        _fakes.Handlers["HasBadgeAsync"] = call =>
            Task.FromResult(
                (string)call.Args[0]! == "ACH_TraderPass1" && _passHolders.Contains((long)call.Key!)
            );
    }

    [Fact]
    public async Task Starting_a_trade_without_the_pass_shows_the_trading_perk_notification()
    {
        _passHolders.Remove(INITIATOR);

        (await NewTradeGrain().OpenAsync(Initiator, PARTNER, Ct)).Should().BeFalse();

        _fakes
            .SentToPlayer<NotificationDialogMessageComposer>(INITIATOR)
            .Should()
            .ContainSingle()
            .Which.NotificationType.Should()
            .Be("trade.trading_perk");
        _fakes.SentToPlayer<TradingOpenEventMessageComposer>(INITIATOR).Should().BeEmpty();
        _fakes.Sent().Should().ContainSingle("the partner hears nothing");
    }

    [Fact]
    public async Task Asking_a_player_without_the_pass_answers_that_they_cannot_trade()
    {
        _passHolders.Remove(PARTNER);

        (await NewTradeGrain().OpenAsync(Initiator, PARTNER, Ct)).Should().BeFalse();

        var failed = _fakes
            .SentToPlayer<TradeOpenFailedEventPaserMessageComposer>(INITIATOR)
            .Should()
            .ContainSingle()
            .Subject;
        ((int)failed.Reason).Should().Be(4, "inventory.trading.openfail.4");
        failed.OtherPlayerName.Should().Be("partner");
        _fakes.SentToPlayer<object>(PARTNER).Should().BeEmpty();
    }

    [Fact]
    public async Task Both_holding_the_pass_open_a_trade_where_both_can_offer()
    {
        (await NewTradeGrain().OpenAsync(Initiator, PARTNER, Ct)).Should().BeTrue();

        var open = _fakes.Sent<TradingOpenEventMessageComposer>().First();
        open.PlayerCanTrade.Should().BeTrue();
        open.OtherPlayerCanTrade.Should().BeTrue();
    }

    [Fact]
    public async Task Receive_only_opens_the_trade_and_refuses_the_offers_of_one_without_the_pass()
    {
        _passHolders.Remove(INITIATOR);
        var trades = NewTradeGrain(TradingPassMode.ReceiveOnly);

        (await trades.OpenAsync(Initiator, PARTNER, Ct)).Should().BeTrue();

        var open = _fakes.SentToPlayer<TradingOpenEventMessageComposer>(PARTNER).Single();
        open.PlayerId.Value.Should().Be(INITIATOR);
        open.PlayerCanTrade.Should().BeFalse("the client shows own_account_disabled to them");
        open.OtherPlayerCanTrade.Should().BeTrue();

        (await trades.AddItemsAsync(Initiator, [10], Ct)).Should().BeFalse();
        _fakes
            .SentToPlayer<TradingYouAreNotAllowedEventMessageComposer>(INITIATOR)
            .Should()
            .ContainSingle();
        _fakes.Sent<TradingItemListEventMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task With_the_pass_off_everyone_trades()
    {
        _passHolders.Clear();

        (await NewTradeGrain(TradingPassMode.Off).OpenAsync(Initiator, PARTNER, Ct))
            .Should()
            .BeTrue();

        var open = _fakes.Sent<TradingOpenEventMessageComposer>().First();
        open.PlayerCanTrade.Should().BeTrue();
        open.OtherPlayerCanTrade.Should().BeTrue();
    }

    [Fact]
    public async Task A_room_without_trading_answers_with_its_own_text()
    {
        _roomAllows = false;

        (await NewTradeGrain().OpenAsync(Initiator, PARTNER, Ct)).Should().BeFalse();

        _fakes
            .SentToPlayer<TradeOpenFailedEventPaserMessageComposer>(INITIATOR)
            .Single()
            .Reason.Should()
            .Be(TradeOpenFailedType.RoomDisallows, "inventory.trading.openfail.6");
    }

    [Fact]
    public async Task A_trade_lock_answers_with_the_lock_text_and_a_locked_partner_cannot_trade()
    {
        _tradeLocked.Add(INITIATOR);
        await NewTradeGrain().OpenAsync(Initiator, PARTNER, Ct);

        _tradeLocked.Clear();
        _tradeLocked.Add(PARTNER);
        await NewTradeGrain().OpenAsync(Initiator, PARTNER, Ct);

        _fakes
            .SentToPlayer<TradeOpenFailedEventPaserMessageComposer>(INITIATOR)
            .Select(x => (int)x.Reason)
            .Should()
            .Equal(3, 4);
    }

    [Fact]
    public async Task A_second_trade_gets_the_already_open_reasons()
    {
        var trades = NewTradeGrain();
        await trades.OpenAsync(Initiator, PARTNER, Ct);

        await trades.OpenAsync(Initiator, PARTNER, Ct);

        // Flash TradingModel.handleMessageEvent: 7 and 8 both show "already open".
        _fakes
            .SentToPlayer<TradeOpenFailedEventPaserMessageComposer>(INITIATOR)
            .Select(x => (int)x.Reason)
            .Should()
            .Equal(7);
    }

    [Fact]
    public async Task A_replaced_trade_policy_decides_who_opens_a_trade()
    {
        _passHolders.Clear();
        var trades = NewTradeGrain();
        GrainHarness.Replace<IPlayerTradePolicy>(trades, new EveryoneTrades());

        (await trades.OpenAsync(Initiator, PARTNER, Ct)).Should().BeTrue();

        _fakes.Sent<NotificationDialogMessageComposer>().Should().BeEmpty();
        _fakes.SentToPlayer<TradingOpenEventMessageComposer>(PARTNER).Should().ContainSingle();
    }

    /// <summary>A hotel's own rule in place of the pass: everyone trades both ways.</summary>
    private sealed class EveryoneTrades : IPlayerTradePolicy
    {
        public Task<TradeStandingType> GetTradeStandingAsync(
            PlayerId playerId,
            CancellationToken ct
        ) => Task.FromResult(TradeStandingType.Full);
    }

    private static ActionContext Initiator => ActionContext.CreateForPlayer(INITIATOR, ROOM);

    private IRoomTradeGrain NewTradeGrain(TradingPassMode mode = TradingPassMode.Required)
    {
        var grain = GrainHarness.Create<RoomTradeGrain>(_fakes);
        GrainHarness.Replace(grain, new RoomTradeLiveState { RoomId = ROOM });
        GrainHarness.Replace<IPlayerTradePolicy>(
            grain,
            new PlayerTradePolicy(
                GrainHarness.Get<IGrainFactory>(grain),
                new TestOptionsMonitor<TradingConfig>(new TradingConfig { PassMode = mode })
            )
        );

        return (IRoomTradeGrain)grain;
    }
}
