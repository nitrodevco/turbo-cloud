using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record UpdateRoomCategoryAndTradeSettingsMessage : IMessageEvent
{
    public RoomId RoomId { get; init; }
    public int CategoryId { get; init; }
    public RoomTradeModeType TradeType { get; init; }
}
