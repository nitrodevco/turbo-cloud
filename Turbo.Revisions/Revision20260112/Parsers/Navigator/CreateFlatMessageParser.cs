using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260112.Parsers.Navigator;

internal class CreateFlatMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new CreateFlatMessage
        {
            FlatName = packet.PopString(),
            FlatDescription = packet.PopString(),
            FlatModelName = packet.PopString(),
            CategoryID = packet.PopInt(),
            MaxPlayers = packet.PopInt(),
            TradeSetting = (RoomTradeModeType)packet.PopInt(),
        };
}
