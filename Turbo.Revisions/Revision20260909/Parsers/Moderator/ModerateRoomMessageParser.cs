using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Moderator;

internal class ModerateRoomMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new ModerateRoomMessage
        {
            RoomId = packet.PopInt(),
            LockDoor = packet.PopInt() == 1,
            ChangeName = packet.PopInt() == 1,
            Kick = packet.PopInt() == 1,
        };
}
