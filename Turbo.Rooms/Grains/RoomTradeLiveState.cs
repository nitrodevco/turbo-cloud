using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Rooms.Grains;

internal sealed class RoomTradeLiveState
{
    public required RoomId RoomId { get; init; }

    /// <summary>Open trades, keyed by both parties so either can be found from a player id.</summary>
    public Dictionary<PlayerId, TradeSession> TradesByPlayerId { get; } = [];
}
