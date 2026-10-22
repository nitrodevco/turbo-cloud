using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room's trade mode decides for the pair, not for each side: in a rights-only room the
/// client offers Trade when either avatar holds rights (InfoStandWidgetHandler, trade mode 1),
/// so the owner can trade a visitor. Before, the room asked each side alone and refused the
/// owner with TradingOtherNotAllowed because the visitor had no rights.
/// </summary>
public sealed class TradeRoomModeTests
{
    private const int OWNER_OBJECT = 1;
    private const int VISITOR_OBJECT = 2;
    private const int OTHER_VISITOR_OBJECT = 3;

    private readonly WiredRoom _room = new(8, 8);

    public TradeRoomModeTests()
    {
        _room.Enter(OWNER_OBJECT, 1, 1, playerId: 1);
        _room.Enter(VISITOR_OBJECT, 2, 2, name: "visitor");
        _room.Enter(OTHER_VISITOR_OBJECT, 3, 3, name: "other");
    }

    private RoomTradeModule Trades => _room.Harness.Module<RoomTradeModule>();

    [Fact]
    public async Task The_owner_can_trade_a_visitor_in_a_rights_only_room()
    {
        UseTradeMode(RoomTradeModeType.RoomOwnerAndRights);

        var parties = await PartiesAsync(OWNER_OBJECT, VISITOR_OBJECT);

        parties.InitiatorMayTrade.Should().BeTrue();
        parties.PartnerMayTrade.Should().BeTrue();
    }

    [Fact]
    public async Task A_visitor_can_trade_the_owner_in_a_rights_only_room()
    {
        UseTradeMode(RoomTradeModeType.RoomOwnerAndRights);

        var parties = await PartiesAsync(VISITOR_OBJECT, OWNER_OBJECT);

        parties.InitiatorMayTrade.Should().BeTrue();
        parties.PartnerMayTrade.Should().BeTrue();
    }

    [Fact]
    public async Task A_visitor_with_rights_can_trade_another_visitor_in_a_rights_only_room()
    {
        UseTradeMode(RoomTradeModeType.RoomOwnerAndRights);
        _room.Harness.State.PlayerIdsWithRights.Add(_room.Avatars[VISITOR_OBJECT].PlayerId);

        var parties = await PartiesAsync(OTHER_VISITOR_OBJECT, VISITOR_OBJECT);

        parties.InitiatorMayTrade.Should().BeTrue();
        parties.PartnerMayTrade.Should().BeTrue();
    }

    [Fact]
    public async Task Two_visitors_without_rights_cannot_trade_in_a_rights_only_room()
    {
        UseTradeMode(RoomTradeModeType.RoomOwnerAndRights);

        var parties = await PartiesAsync(VISITOR_OBJECT, OTHER_VISITOR_OBJECT);

        parties.InitiatorMayTrade.Should().BeFalse();
        parties.PartnerMayTrade.Should().BeFalse();
    }

    [Fact]
    public async Task Nobody_can_trade_in_a_room_with_trading_off()
    {
        UseTradeMode(RoomTradeModeType.Disabled);

        var parties = await PartiesAsync(OWNER_OBJECT, VISITOR_OBJECT);

        parties.InitiatorMayTrade.Should().BeFalse();
        parties.PartnerMayTrade.Should().BeFalse();
    }

    private void UseTradeMode(RoomTradeModeType mode) =>
        _room.Harness.State.RoomSnapshot = _room.Harness.State.RoomSnapshot with
        {
            TradeType = mode,
        };

    private async Task<TradePartiesSnapshot> PartiesAsync(int initiatorObject, int partnerObject) =>
        (
            await Trades.GetPartiesAsync(
                _room.ContextFor(initiatorObject),
                _room.Avatars[partnerObject].ObjectId
            )
        )!;
}
