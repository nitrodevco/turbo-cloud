using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Rooms;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record UpdateRoomCategoryAndTradeSettingsComposer : IMessageEvent
{
    public int RoomId { get; init; }
    public int CategoryId { get; init; }
    public RoomTradeType TradeType { get; init; }
}
