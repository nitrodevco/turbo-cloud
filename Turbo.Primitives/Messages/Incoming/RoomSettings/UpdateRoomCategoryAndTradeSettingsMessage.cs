using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record UpdateRoomCategoryAndTradeSettingsMessage : IMessageEvent
{
    public int RoomId { get; init; }
    public int CategoryId { get; init; }
    public RoomTradeModeType TradeType { get; init; }
}
