using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.RoomSettings;

internal class GetRoomSettingsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetRoomSettingsMessage { RoomId = packet.PopInt() };
}
