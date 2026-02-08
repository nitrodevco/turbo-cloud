using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260112.Parsers.RoomSettings;

internal class UpdateRoomCategoryAndTradeSettingsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new UpdateRoomCategoryAndTradeSettingsMessage
        {
            RoomId = packet.PopInt(),
            CategoryId = packet.PopInt(),
            TradeType = (RoomTradeModeType)packet.PopInt(),
        };
}
