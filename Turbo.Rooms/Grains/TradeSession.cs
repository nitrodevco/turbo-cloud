using System.Collections.Generic;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Trading.Enums;

namespace Turbo.Rooms.Grains;

/// <summary>A trade between two players in the room: each side's offer and where it stands.</summary>
public sealed class TradeSession
{
    public required PlayerId InitiatorId { get; init; }
    public required PlayerId PartnerId { get; init; }
    public TradeStateType State { get; set; } = TradeStateType.Open;
    public TradeSide InitiatorSide { get; } = new();
    public TradeSide PartnerSide { get; } = new();

    public bool Involves(PlayerId playerId) => playerId == InitiatorId || playerId == PartnerId;

    public PlayerId OtherOf(PlayerId playerId) => playerId == InitiatorId ? PartnerId : InitiatorId;

    public TradeSide SideOf(PlayerId playerId) =>
        playerId == InitiatorId ? InitiatorSide : PartnerSide;

    public bool BothAccepted => InitiatorSide.Accepted && PartnerSide.Accepted;

    public bool BothConfirmed => InitiatorSide.Confirmed && PartnerSide.Confirmed;
}
