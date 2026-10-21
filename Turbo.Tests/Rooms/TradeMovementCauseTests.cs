using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A completed trade is one operation whose cause ref is the room it was made in, so a ledger can
/// place a trade without asking the room; each half's items are that half's movements.
/// </summary>
public sealed class TradeMovementCauseTests
{
    private const int ROOM = 5;
    private const int INITIATOR = 1;
    private const int PARTNER = 2;

    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public TradeMovementCauseTests()
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
        _fakes.Handlers[nameof(IInventoryGrain.TransferFurnitureAsync)] = _ =>
            Task.FromResult(true);
    }

    [Fact]
    public async Task A_completed_trade_is_one_operation_caused_by_its_room()
    {
        var trades = NewTradeGrain();
        var initiator = ActionContext.CreateForPlayer(INITIATOR, ROOM);
        var partner = ActionContext.CreateForPlayer(PARTNER, ROOM);

        await trades.OpenAsync(initiator, 2, Ct);
        await trades.AcceptAsync(initiator, true, Ct);
        await trades.AcceptAsync(partner, true, Ct);
        await trades.ConfirmAsync(initiator, true, Ct);
        await trades.ConfirmAsync(partner, true, Ct);

        var halves = _fakes
            .Log.Of(nameof(IInventoryGrain.TransferFurnitureAsync))
            .Select(call => (MovementCause)call.Args[2]!)
            .ToList();

        halves.Should().HaveCount(2, "the initiator's half, then the partner's");
        halves
            .Select(x => (x.Cause, x.CauseRef, x.ActorPlayerId))
            .Distinct()
            .Should()
            .Equal((MovementCauses.TRADE, ROOM.ToString(), PARTNER));
        halves.Select(x => x.OperationId).Distinct().Should().ContainSingle();
    }

    private IRoomTradeGrain NewTradeGrain()
    {
        var grain = GrainHarness.Create<RoomTradeGrain>(_fakes);
        GrainHarness.Replace(grain, new RoomTradeLiveState { RoomId = ROOM });

        return (IRoomTradeGrain)grain;
    }
}
