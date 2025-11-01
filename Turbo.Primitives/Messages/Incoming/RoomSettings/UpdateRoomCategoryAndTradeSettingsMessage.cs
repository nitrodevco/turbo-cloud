using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Navigator;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record UpdateRoomCategoryAndTradeSettingsMessage : IMessageEvent
{
    public int RoomId { get; init; }
    public int CategoryId { get; init; }
    public TradeModeType TradeType { get; init; }
}
